using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Azure;

using Azure.Storage.Queues;
using Azure.Storage.Blobs;
using Azure.Core.Extensions;

using ShogiEngine;
using ShogiComms;
using Microsoft.Extensions.Logging;
using Azure.Core;
using System.Text.Json;
using Microsoft.Azure.SignalR.Management;

namespace ShogiServerless
{
    public interface IShogiClient
    {
        Task ReceiveGameList(List<ClientGameInfo> list);

        Task ReceiveGameStart(string serializedGame, ClientGameInfo gameInfo, Guid playerId);

        Task ReceiveGameUpdate(string serializedGame, Guid gameId);

        // indicate the other player disconncted
        Task ReceiveGameDisconnect(Guid gameId);

        // indicate that the other player reconnected
        Task ReceiveGameReconnect(Guid gameId);

        Task Echo(string message);
    }

    public class ShogiHub : ServerlessHub<IShogiClient>
    {
        internal static TableStorage TableStorage { get; } = new TableStorage();

        internal class GameInfo : ITableEntity
        {
            internal class PlayerInfo
            {
                public PlayerInfo(string name) => PlayerName = name;
                public PlayerInfo(string name, Guid id) => (PlayerName, PlayerId) = (name, id);

                public Guid PlayerId { get; } = Guid.NewGuid();

                public string PlayerName { get; } = string.Empty;

                public override string ToString() => $"<{PlayerName}-{PlayerId}>";

            }

            private TaikyokuShogi? _game;

            public TaikyokuShogi Game { get => _game ?? throw new NullReferenceException(); }

            public Guid Id { get; private set; }

            public DateTime Created { get; private set; }

            public DateTime LastPlayed { get; set; }

            public PlayerInfo? BlackPlayer { get; private set; } = null;

            public PlayerInfo? WhitePlayer { get; private set; } = null;

            public bool IsOpen { get => BlackPlayer == null || WhitePlayer == null; }

            public GameInfo(TaikyokuShogi game, string playerName, PlayerColor color)
            {
                (_game, Id, Created, LastPlayed) = (game, Guid.NewGuid(), DateTime.UtcNow, DateTime.Now);
                var playerInfo = new PlayerInfo(playerName);
                if (color == PlayerColor.Black)
                    BlackPlayer = playerInfo;
                else
                    WhitePlayer = playerInfo;
                (((ITableEntity)this).PartitionKey, ((ITableEntity)this).RowKey) = (string.Empty, Id.ToString());
            }

            public (PlayerInfo oldPlayer, PlayerInfo newPlayer) AddPlayer(string name)
            {
                if (BlackPlayer == null && WhitePlayer != null)
                {
                    BlackPlayer = new PlayerInfo(name);
                    return (WhitePlayer, BlackPlayer);
                }
                
                if (WhitePlayer == null && BlackPlayer != null)
                {
                    WhitePlayer = new PlayerInfo(name);
                    return (BlackPlayer, WhitePlayer);
                }
                
                throw new HubException($"Failed to join game: {Id}");
            }

            public PlayerColor GetPlayerColor(Guid playerId) => 
                playerId == BlackPlayer?.PlayerId ? PlayerColor.Black :
                    (playerId == WhitePlayer?.PlayerId ? PlayerColor.White :
                        throw new HubException("unknown player"));

            public PlayerInfo GetPlayerInfo(Guid playerId) => GetPlayerInfo(GetPlayerColor(playerId)) ?? throw new Exception("unkonwn player");

            public PlayerInfo? GetPlayerInfo(PlayerColor player) =>
                player switch
                {
                    PlayerColor.Black => BlackPlayer,
                    PlayerColor.White => WhitePlayer,
                    _ => throw new HubException("unknown player")
                };

            public PlayerInfo? GetOtherPlayerInfo(Guid playerId) =>
                GetPlayerInfo(GetPlayerColor(playerId).Opponent());

            // Convert the saved state of this game into information that the client can consume
            public ClientGameInfo ToClientGameInfo() =>
                new ClientGameInfo()
                {
                    GameId = Id,
                    Created = Created,
                    LastPlayed = LastPlayed,
                    BlackName = BlackPlayer?.PlayerName,
                    WhiteName = WhitePlayer?.PlayerName
                };

            //
            // implmenting ITableEntity
            //

            // parameterlesss contructor requred for ITableEntry
            public GameInfo() { }

            public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
            {
                return new Dictionary<string, EntityProperty>
                {
                    ["Game"] = new EntityProperty(Game.Serialize()),
                    ["Id"] = new EntityProperty(Id),
                    ["Created"] = new EntityProperty(Created),
                    ["LastPlayed"] = new EntityProperty(LastPlayed),
                    ["BlackPlayer_PlayerId"] = new EntityProperty(BlackPlayer?.PlayerId ?? Guid.Empty),
                    ["BlackPlayer_PlayerName"] = new EntityProperty(BlackPlayer?.PlayerName ?? ""),
                    ["WhitePlayer_PlayerId"] = new EntityProperty(WhitePlayer?.PlayerId ?? Guid.Empty),
                    ["WhitePlayer_PlayerName"] = new EntityProperty(WhitePlayer?.PlayerName ?? "")
                };
            }

            public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
            {
                _game = TaikyokuShogi.Deserlialize(properties["Game"].BinaryValue);
                Id = properties["Id"].GuidValue ?? throw new Exception("Cannot deserialize");
                Created = properties["Created"].DateTime ?? throw new Exception("Cannot deserialize");
                LastPlayed = properties["LastPlayed"].DateTime ?? throw new Exception("Cannot deserialize");

                var blackId = properties["BlackPlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
                if (blackId != Guid.Empty)
                    BlackPlayer = new PlayerInfo(properties["BlackPlayer_PlayerName"].StringValue ?? throw new Exception("Cannot deserialize"), blackId);
                var whiteId = properties["WhitePlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
                if (whiteId != Guid.Empty)
                    WhitePlayer = new PlayerInfo(properties["WhitePlayer_PlayerName"].StringValue ?? throw new Exception("Cannot deserialize"), whiteId);
            }

            string? ITableEntity.PartitionKey { get; set; }

            string? ITableEntity.RowKey { get; set; }

            string? ITableEntity.ETag { get; set; }

            DateTimeOffset ITableEntity.Timestamp { get; set; }
        }

        internal class ConnectionMap
        {
            private Dictionary<string, (Guid GameId, Guid PlayerId)> _connectionToPlayer { get; } = new Dictionary<string, (Guid GameId, Guid PlayerId)>();
            private Dictionary<(Guid GameId, Guid PlayerId), string> _playerToConnection { get; } = new Dictionary<(Guid GameId, Guid PlayerId), string>();

            private readonly object _lock = new object();

            public bool TryGetConnection(Guid gameId, Guid playerId, out string connectionId)
            {
                lock (_lock)
                {
                    var success = _playerToConnection.TryGetValue((gameId, playerId), out var value);
                    connectionId = success ? value ?? string.Empty : string.Empty;
                    return success;
                }
            }

            public bool TryGetPlayer(string connectionId, out Guid gameId, out Guid playerId)
            {
                lock (_lock)
                {
                    var success = _connectionToPlayer.TryGetValue(connectionId, out var pair);
                    (gameId, playerId) = success ? pair : (Guid.Empty, Guid.Empty);
                    return success;
                }
            }

            // remove connectionId from mappings
            // Returns previous connection mapping to (gameId, playerId) if it exists
            public (Guid GameId, Guid PlayerId)? UnmapConnection(string connectionId)
            {
                lock (_lock)
                {
                    if (_connectionToPlayer.TryGetValue(connectionId, out var oldGamePlayerPair))
                    {
                        _playerToConnection.Remove(oldGamePlayerPair);
                        _connectionToPlayer.Remove(connectionId);
                    }
                    return oldGamePlayerPair == (Guid.Empty, Guid.Empty) ? null as (Guid, Guid)? : oldGamePlayerPair;
                }
            }

            // Creates the mapping: connectionId <==> (game, player)
            // Returns previous mamppings for both connectionId and (game, player)
            public (string? OldConnection, (Guid GameId, Guid PlayerId)? OldGamePlayerPair) MapConnection(string connectionId, Guid gameId, Guid playerId)
            {
                lock (_lock)
                {
                    // remove stale values
                    //
                    // INITIAL         map(A, g, p)             map(B, x, y)
                    //  A => (x, y)      A => (g, p)              A => (x, y)  <-- stale
                    //  (x,y) => A       (x,y) => A  <-- stale    (x, y) => B
                    //                   (g,p) => A               B => (x, y)
                    //
                    if (_connectionToPlayer.TryGetValue(connectionId, out var oldGamePlayerPair))
                    {
                        _playerToConnection.Remove(oldGamePlayerPair);
                    }

                    if (_playerToConnection.TryGetValue((gameId, playerId), out var oldConnection))
                    {
                        _connectionToPlayer.Remove(oldConnection);
                    }

                    // map new values: Connection <=> (Game, Player)
                    _connectionToPlayer[connectionId] = (gameId, playerId);
                    _playerToConnection[(gameId, playerId)] = connectionId;
                    return (oldConnection, oldGamePlayerPair == (Guid.Empty, Guid.Empty) ? null as (Guid, Guid)? : oldGamePlayerPair);
                }
            }
        }

        private async Task SignalDisconnectToOpponent(Guid gameId, Guid playerId, ILogger logger)
        {
            var gameInfo = await TableStorage.FindGame(gameId)
                ?? throw new HubException($"failed to find game: {gameId}");
            var otherPlayerInfo = gameInfo.GetOtherPlayerInfo(playerId);

            if (otherPlayerInfo != null &&
                    TryGetConnection(gameId, otherPlayerInfo.PlayerId, out var otherConnection, logger))
            {
                logger.LogInformation($"sending disconnect to '{otherConnection}' for '{gameId}'/'{otherPlayerInfo.PlayerId}'");
                await Clients.Client(otherConnection).ReceiveGameDisconnect(gameInfo.Id);
            }
        }

        private async Task UnmapConnection(string connectionId, ILogger logger)
        {
            logger.LogInformation($"unmapping connection '{connectionId}'");
            var stalePair = _connectionMap.UnmapConnection(connectionId);

            if (stalePair != null)
            {
                Guid gameId = stalePair.Value.GameId;
                Guid playerId = stalePair.Value.PlayerId;
                await SignalDisconnectToOpponent(gameId, playerId, logger);
            }
        }

        private async Task MapConnection(string connectionId, Guid gameId, Guid playerId, ILogger logger)
        {
            logger.LogInformation($"mapping '{connectionId}' to '{gameId}/{playerId}'");

            var (oldConnection, oldGamePlayerPair) = _connectionMap.MapConnection(connectionId, gameId, playerId);

            if (oldGamePlayerPair != null)
            {
                var oldGameId = (Guid)oldGamePlayerPair?.GameId!;
                var oldPlayerId = (Guid)oldGamePlayerPair?.PlayerId!;
                logger.LogInformation($"'{connectionId}' previously mapped to '{oldGameId}'/'{oldPlayerId}'");
                await SignalDisconnectToOpponent(oldGameId, oldPlayerId, logger);
            }

            if (oldConnection != null)
            {
                logger.LogInformation($"'{gameId}'/'{playerId}' prevoiusly mapped to '{connectionId}'");
                await SignalDisconnectToOpponent(gameId, playerId, logger);

                logger.LogInformation($"closing stale connection '{oldConnection}'");
                await ClientManager.CloseConnectionAsync(oldConnection, "new connection created");
            }
        }

        private static bool TryGetConnection(Guid gameId, Guid playerId, out string connectionId, ILogger logger)
        {
            bool result = _connectionMap.TryGetConnection(gameId, playerId, out connectionId);
            logger.LogInformation(result ? $"mapped '{gameId}/{playerId}' to '{connectionId}'"
                : $"no connection map found for '{gameId}/{playerId}'");
            return result;
        }

        private static bool TryGetPlayer(string connectionId, out Guid gameId, out Guid playerId, ILogger logger)
        {
            bool result = _connectionMap.TryGetPlayer(connectionId, out gameId, out playerId);
            logger.LogInformation(result ? $"mapped '{connectionId}' to '{gameId}/{playerId}'"
                : $"no connection map found for '{connectionId}'");
            return result;
        }

        private static async Task<IEnumerable<ClientGameInfo>> AllOpenGames() =>
            await TableStorage.AllGames().ContinueWith(t =>
            {
                var gameList = new List<ClientGameInfo>();
                foreach (var game in t.Result)
                {
                    if (game.IsOpen)
                    {
                        gameList.Add(game.ToClientGameInfo());
                    }
                }
                return gameList as IEnumerable<ClientGameInfo>;
            });

        private static readonly ConnectionMap _connectionMap = new ConnectionMap();

        public ShogiHub() : base() { }

        [FunctionName(nameof(CreateGame))]
        public async Task<GamePlayerPair> CreateGame([SignalRTrigger] InvocationContext context,
            string playerName, bool asBlackPlayer, string existingGameSerialized, ILogger logger)
        {
            var game = JsonSerializer.Deserialize<TaikyokuShogi>(existingGameSerialized ?? "")
                ?? throw new HubException("failed to deserialize game state");
            var gameInfo = new GameInfo(game, playerName, asBlackPlayer ? PlayerColor.Black : PlayerColor.White);
            gameInfo = TableStorage.AddOrUpdateGame(gameInfo)
                ?? throw new HubException("failed to add game state");

            logger.LogInformation($"client '{context.ConnectionId}' creaging new game created with id '{gameInfo.Id}'");

            logger.LogInformation("sending attached clients updated game list");
            await AllOpenGames().ContinueWith(t => Clients.All.ReceiveGameList(t.Result.ToList()));

            var playerId = gameInfo.GetPlayerInfo(asBlackPlayer ? PlayerColor.Black : PlayerColor.White)?.PlayerId ?? throw new HubException("bad game state");

            await MapConnection(context.ConnectionId, gameInfo.Id, playerId, logger);

            return new GamePlayerPair(gameInfo.Id, playerId);
        }

        [FunctionName(nameof(RequestAllOpenGameInfo))]
        public async Task<IEnumerable<ClientGameInfo>> RequestAllOpenGameInfo([SignalRTrigger] InvocationContext context)
        {
            // query the table storage for all the requested games
            return await TableStorage.AllGames().ContinueWith(t => {
                var gameList = new List<ClientGameInfo>();
                foreach (var game in t.Result)
                {
                    if (game.IsOpen)
                    {
                        gameList.Add(game.ToClientGameInfo());
                    }
                }
                return gameList as IEnumerable<ClientGameInfo>;
            });
        }

        [FunctionName(nameof(RequestGameInfo))]
        public async Task<IEnumerable<ClientGameInfo>> RequestGameInfo([SignalRTrigger] InvocationContext context,
            NetworkGameRequestList requests)
        {
            var gameList = new ConcurrentBag<ClientGameInfo>();

            // query the table storage for all the requested games
            var tasks = new List<Task>();
            foreach (var request in requests.List)
            {
                tasks.Add(TableStorage.FindGame(request.GameId).
                    ContinueWith(t =>
                    {
                        var gameInfo = t.Result;
                        if (gameInfo != null)
                            gameList.Add(gameInfo.ToClientGameInfo());
                    }));
            }

            return await Task.WhenAll(tasks.ToArray()).ContinueWith(t =>
            {
                return gameList as IEnumerable<ClientGameInfo>;
            });
        }

        [FunctionName(nameof(JoinGame))]
        public async Task JoinGame([SignalRTrigger] InvocationContext context, 
            Guid gameId, string playerName,
            ILogger logger)
        {
            logger.LogInformation($"client '{context.ConnectionId}' named '{playerName}' requesting to join game '{gameId}'");

            var gameInfo = await TableStorage.FindGame(gameId)
                ?? throw new HubException($"failed to find game: {gameId}");

            var (oldPlayerInfo, newPlayerInfo) = gameInfo.AddPlayer(playerName);
            gameInfo = TableStorage.AddOrUpdateGame(gameInfo)
                ?? throw new HubException("Internal Storage Error: Unable to connect game");

            logger.LogInformation($"'{context.ConnectionId}' sucessfully joined '{gameId}'");

            logger.LogInformation($"sending all clients updated open game list");
            await AllOpenGames().ContinueWith(t => Clients.All.ReceiveGameList(t.Result.ToList()));

            // add new connection to the map
            await MapConnection(context.ConnectionId, gameId, newPlayerInfo.PlayerId, logger);

            // signal other player game has started
            if (TryGetConnection(gameId, oldPlayerInfo.PlayerId, out var otherConnection, logger))
            {
                logger.LogInformation($"signal player '{oldPlayerInfo}' game start for '{gameId}'");
                await Clients.Client(otherConnection).
                    ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.ToClientGameInfo(), oldPlayerInfo.PlayerId);
            }

            // signal joining player game has started
            logger.LogInformation($"signal player '{newPlayerInfo}' game start for '{gameId}'");
            await Clients.Client(context.ConnectionId).
                ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.ToClientGameInfo(), newPlayerInfo.PlayerId);
        }

        [FunctionName(nameof(RejoinGame))]
        public async Task RejoinGame([SignalRTrigger] InvocationContext context,
            Guid gameId, Guid playerId,
            ILogger logger)
        {
            var gameInfo = await TableStorage.FindGame(gameId)
                ?? throw new HubException($"failed to find game: {gameId}");
            var otherPlayerInfo = gameInfo.GetOtherPlayerInfo(playerId);

            await MapConnection(context.ConnectionId, gameId, playerId, logger);

            if (otherPlayerInfo != null)
            {
                logger.LogInformation($"other player for '{gameId}' is '{otherPlayerInfo.PlayerId}'");
                if (TryGetConnection(gameId, otherPlayerInfo.PlayerId, out var otherConnection, logger))
                {
                    logger.LogInformation($"signal other connection '{otherConnection}' there is a reconnect for '{gameId}'");
                    await Clients.Client(otherConnection).ReceiveGameReconnect(gameId);
                }
            }
            else
            {
                logger.LogInformation($"no other player connected to '{gameId}'");
            }

            logger.LogInformation($"signal this player there is a game start for '{gameId}'");
            await Clients.Client(context.ConnectionId).ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.ToClientGameInfo(), playerId);
        }

        // Record updated game state into persistant storage and notify any attached clients
        private async Task UpdateGame(GameInfo gameInfo,
            ILogger logger)
        {
            gameInfo = TableStorage.AddOrUpdateGame(gameInfo)
                ?? throw new HubException("Interal Server error: cannot record move");

            var blackPlayerId = gameInfo.BlackPlayer?.PlayerId ?? throw new HubException("game in bad state; missing black player");
            var whitePlayerId = gameInfo.WhitePlayer?.PlayerId ?? throw new HubException("game in bad state; missing white player");

            logger.LogInformation($"Black player is '{gameInfo.Id}/{blackPlayerId}'");
            logger.LogInformation($"White player is '{gameInfo.Id}/{whitePlayerId}'");

            if (TryGetConnection(gameInfo.Id, blackPlayerId, out var connectionBlack, logger))
            {
                logger.LogInformation($"Updating black player'{gameInfo.Id}/{blackPlayerId}' at '{connectionBlack}'");
                await Clients.Client(connectionBlack).ReceiveGameUpdate(gameInfo.Game.ToJsonString(), gameInfo.Id);
            }

            if (TryGetConnection(gameInfo.Id, whitePlayerId, out var connectionWhite, logger))
            {
                logger.LogInformation($"Updating white Player '{gameInfo.Id}/{whitePlayerId}' at '{connectionWhite}'");
                await Clients.Client(connectionWhite).ReceiveGameUpdate(gameInfo.Game.ToJsonString(), gameInfo.Id);
            }
        }

        [FunctionName(nameof(MakeMove))]
        public async Task MakeMove([SignalRTrigger] InvocationContext context,
            Location startLoc, Location endLoc, Location midLoc, bool promote,
            ILogger logger)
        {
            if (!TryGetPlayer(context.ConnectionId, out var gameId, out var playerId, logger))
                return;

            var gameInfo = await TableStorage.FindGame(gameId)
                ?? throw new HubException($"failed to find game: {gameId}");

            if (gameInfo.Game.CurrentPlayer != gameInfo.GetPlayerColor(playerId))
                throw new HubException("illegal move: wrong client requested the move");

            try
            {
                gameInfo.Game.MakeMove(((int, int))startLoc, ((int, int))endLoc, ((int, int)?)midLoc, promote);
                gameInfo.LastPlayed = DateTime.Now;
            }
            catch (InvalidOperationException e)
            {
                throw new HubException("invalid move: unable to complete move", e);
            }

            await UpdateGame(gameInfo, logger);
        }

        [FunctionName(nameof(Resign))]
        public async Task Resign([SignalRTrigger] InvocationContext context, ILogger logger)
        {
            if (!TryGetPlayer(context.ConnectionId, out var gameId, out var playerId, logger))
                return;

            var gameInfo = await TableStorage.FindGame(gameId)
                ?? throw new HubException($"failed to find game: {gameId}");

            gameInfo.Game.Resign(gameInfo.GetPlayerColor(playerId));
            gameInfo.LastPlayed = DateTime.Now;

            await UpdateGame(gameInfo, logger);
        }


#if DEBUG
        [FunctionName(nameof(Echo))]
        public async Task Echo([SignalRTrigger] InvocationContext context, string message, ILogger logger)
        {
            logger.LogInformation($"echoing '{message}' back to '{context.ConnectionId}'");
            await Clients.Client(context.ConnectionId).Echo(message);
        }

        [FunctionName(nameof(TestGameStart))]
        public async Task TestGameStart([SignalRTrigger] InvocationContext context,
            Guid gameId, Guid playerId, string serializedGame,
            ILogger logger)
        {
            var game = serializedGame.ToTaikyokuShogi();
            logger.LogInformation($"testing GameStart message for '{context.ConnectionId}'");

            var info = new ClientGameInfo()
            {
                GameId = gameId,
                BlackName = "blackPlayerFakeName",
                WhiteName = "whitePlayerFakeName"
            };
            await Clients.Client(context.ConnectionId).ReceiveGameStart(game.ToJsonString(), info, playerId);
        }
#endif

        [FunctionName(nameof(OnConnected))]
        public void OnConnected([SignalRTrigger] InvocationContext context, ILogger logger)
        {
            logger.LogInformation($"{context.ConnectionId} has connected");
        }

        [FunctionName(nameof(OnDisconnected))]
        public async Task OnDisconnected([SignalRTrigger] InvocationContext context, ILogger logger)
        {
            await UnmapConnection(context.ConnectionId, logger);
            logger.LogInformation($"{context.ConnectionId} has disconnected");
        }

        [FunctionName("negotiate")]
        public async Task<SignalRConnectionInfo> Negotiate([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req)
            => await NegotiateAsync(new NegotiationOptions());

#if false

        public async Task CancelGame()
        {
            OpenGames.TryRemove(ClientGame.Id, out _);
            await Clients.All.ReceiveGameList(AllOpenGames());
        }

#endif

    }
}