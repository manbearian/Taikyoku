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

            private object _lock = new object();

            public string? GetConnection(Guid gameId, Guid playerId)
            {
                lock (_lock)
                {
                    return (_playerToConnection.TryGetValue((gameId, playerId), out var connectionId)) ? connectionId : null; 
                }
            }

            // Returns connectionId of the previous connection mapped to (gameId, playerId) if it exists
            public string? MapConnection(string connectionId, Guid gameId, Guid playerId)
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
                    return oldConnection;
                }
            }
        }

        private void MapConnection(string connectionId, Guid gameId, Guid playerId, ILogger logger)
        {
            logger.LogInformation($"mapping '{connectionId}' to '{gameId}-{playerId}'");

            var staleConnection = _connectionMap.MapConnection(connectionId, gameId, playerId);
            if (staleConnection != null)
            {
                logger.LogInformation($"closing stale connection '{staleConnection}'");
                ClientManager.CloseConnectionAsync(staleConnection, "new connection created");
            }
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

        private static ConnectionMap _connectionMap = new ConnectionMap();

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

            MapConnection(context.ConnectionId, gameInfo.Id, playerId, logger);

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
            MapConnection(context.ConnectionId, gameId, newPlayerInfo.PlayerId, logger);

            // signal other player game has started
            var otherConnection = _connectionMap.GetConnection(gameId, oldPlayerInfo.PlayerId);
            if (otherConnection != null)
            {
                logger.LogInformation($"signal player '{oldPlayerInfo}' game start for '{gameId}'");
                await Clients.Client(otherConnection).
                    ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.ToClientGameInfo(), oldPlayerInfo.PlayerId);
            }
            else
            {
                logger.LogInformation($"other player not connected to game '{gameId}'");
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

            Guid otherPlayerId = Guid.Empty;
            if (gameInfo.WhitePlayer?.PlayerId == playerId)
                otherPlayerId = gameInfo.BlackPlayer?.PlayerId ?? Guid.Empty;
            else if (gameInfo.BlackPlayer?.PlayerId == playerId)
                otherPlayerId = gameInfo.WhitePlayer?.PlayerId ?? Guid.Empty;
            else
                throw new HubException($"unknown player '{playerId}' attempting to join '{gameId}'");

            MapConnection(context.ConnectionId, gameId, playerId, logger);

            if (otherPlayerId != Guid.Empty)
            {
                logger.LogInformation($"other player for '{gameId}' is '{otherPlayerId}'");
                var otherConnection = _connectionMap.GetConnection(gameId, otherPlayerId);
                if (otherConnection != null)
                {
                    logger.LogInformation($"signal other connection '{otherConnection}' there is a reconnect for '{gameId}'");
                    await Clients.Client(otherConnection).ReceiveGameReconnect(gameId);
                } else
                {
                    logger.LogInformation($"no connection for '{gameId}-{otherPlayerId}'");

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

            logger.LogInformation($"Black player is '{gameInfo.Id}-{blackPlayerId}'");
            logger.LogInformation($"White player is '{gameInfo.Id}-{whitePlayerId}'");

            var connectionBlack = _connectionMap.GetConnection(gameInfo.Id, blackPlayerId);
            if (connectionBlack != null)
            {
                logger.LogInformation($"Updating black player'{gameInfo.Id}-{blackPlayerId}' at '{connectionBlack}'");
                await Clients.Client(connectionBlack).ReceiveGameUpdate(gameInfo.Game.ToJsonString(), gameInfo.Id);
            }
            else
            {
                logger.LogInformation($"no connection for black player '{gameInfo.Id}-{blackPlayerId}'");
            }

            var connectionWhite = _connectionMap.GetConnection(gameInfo.Id, whitePlayerId);
            if (connectionWhite != null)
            {
                logger.LogInformation($"Updating white Player '{gameInfo.Id}-{whitePlayerId}' at '{connectionWhite}'");
                await Clients.Client(connectionWhite).ReceiveGameUpdate(gameInfo.Game.ToJsonString(), gameInfo.Id);
            }
            else
            {
                logger.LogInformation($"no connection for white player '{gameInfo.Id}-{whitePlayerId}'");
            }
        }

        public async Task MakeMove([SignalRTrigger] InvocationContext context,
            Guid gameId, Guid playerId, Location startLoc, Location endLoc, Location midLoc, bool promote,
            ILogger logger)
        {
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

#if DEBUG
        [FunctionName(nameof(Echo))]
        public async Task Echo([SignalRTrigger] InvocationContext context, string message, ILogger logger)
        {
            logger.LogInformation($"echoing '{message}' back to '{context.ConnectionId}'");
            await Clients.Client(context.ConnectionId).Echo(message);
        }

        [FunctionName(nameof(TestGameStart))]
        public async Task TestGameStart([SignalRTrigger] InvocationContext context, string serializedGame, ILogger logger)
        {
            var game = serializedGame.ToTaikyokuShogi();
            logger.LogInformation($"testing GameStart message for '{context.ConnectionId}'");

            var info = new ClientGameInfo()
            {
                GameId = Guid.NewGuid(),
                BlackName = "blackPlayerFakeName",
                WhiteName = "whitePlayerFakeName"
            };
            await Clients.Client(context.ConnectionId).ReceiveGameStart(game.ToJsonString(), info, Guid.NewGuid());
        }
#endif

        [FunctionName(nameof(OnConnected))]
        public void OnConnected([SignalRTrigger] InvocationContext context, ILogger logger)
        {
            logger.LogInformation($"{context.ConnectionId} has connected");
        }

        [FunctionName(nameof(OnDisconnected))]
        public void OnDisconnected([SignalRTrigger] InvocationContext context, ILogger logger)
        {
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

        public async Task RequestResign()
        {
            var gameInfo = ClientGame;
            var playerId = ClientPlayerId;

            if (gameInfo == null)
                throw new HubException("illegal move: no game in progress");

            gameInfo.Game.Resign(gameInfo.GetPlayerColor(playerId));
            gameInfo.LastPlayed = DateTime.Now;

            await UpdateGame(gameInfo);
        }

        private async Task DisconnectClientGame()
        {
            var gameInfo = ClientGame;
            var playerId = ClientPlayerId;

            if (gameInfo == null)
                return;

            if (OpenGames.ContainsKey(gameInfo.Id))
            {
                // if the game is pending (no opponent connected yet) remove it from list of pending games
                await CancelGame();
                return;
            }

            // TODO: when upgrading to .net 5 we can use overload of TryRemove that takes a value and remove this lock
            lock (ClientMapLock)
            {
                // remove this client from the map if it is still relavant (see RACE below)
                // RACE: a new client can connect as this player before the disconnection of the older client occurs
                if (ClientMap[playerId] == (Clients.Caller, Context.ConnectionId))
                    ClientMap.TryRemove(playerId, out _);
            }

            var otherPlayerInfo = gameInfo.GetOtherPlayerInfo(playerId);
            var otherClient = ClientMap.GetValueOrDefault(otherPlayerInfo.PlayerId).Client;
            if (otherClient != null)
                await otherClient.ReceiveGameDisconnect(gameInfo.Id);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await DisconnectClientGame();
            await base.OnDisconnectedAsync(exception);
        }
#endif

    }
}