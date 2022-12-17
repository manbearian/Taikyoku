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

        Task ReceiveGameStart(string serializedGame, Guid gameId,  Guid playerId);

        Task ReceiveGameUpdate(string serializedGame, Guid id);

        // indicate the other player disconncted
        Task ReceiveGameDisconnect(Guid id);

        // indicate that the other player reconnected
        Task ReceiveGameReconnect(Guid id);

        Task Echo(string message);
    }

    public class ShogiHub : ServerlessHub<IShogiClient>
    {
        internal static TableStorage TableStorage { get; } = new TableStorage();

        internal class GameInfo : ITableEntity
        {
            internal class PlayerInfo
            {
                public PlayerInfo() { }

                public PlayerInfo(string name)
                {
                    PlayerId = Guid.NewGuid();
                    PlayerName = name;
                }

                public Guid PlayerId { get; set; } = Guid.Empty;

                public string PlayerName { get; set; } = string.Empty;

                public override string ToString() => $"<{PlayerName}-{PlayerId}>";

            }

            private TaikyokuShogi? _game;

            public TaikyokuShogi Game { get => _game ?? throw new NullReferenceException(); }

            public Guid Id { get; private set; }

            public DateTime Created { get; private set; }

            public DateTime LastPlayed { get; set; }

            public PlayerInfo BlackPlayer { get; private set; } = new PlayerInfo();

            public PlayerInfo WhitePlayer { get; private set; } = new PlayerInfo();

            public bool IsOpen { get => BlackPlayer.PlayerId == Guid.Empty || WhitePlayer.PlayerId == Guid.Empty; }

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

            public PlayerColor GetPlayerColor(Guid playerId) => 
                playerId == BlackPlayer.PlayerId ? PlayerColor.Black :
                    (playerId == WhitePlayer.PlayerId ? PlayerColor.White :
                        throw new HubException("unknown player"));

            public (PlayerInfo oldPlayer, PlayerInfo newPlayer) AddPlayer(string name)
            {
                if (BlackPlayer.PlayerId == Guid.Empty)
                {
                    if (WhitePlayer.PlayerId == Guid.Empty)
                    {
                        throw new HubException($"Failed to join game, game is abandoned: {Id}");
                    }

                    BlackPlayer = new PlayerInfo(name);
                    return (WhitePlayer, BlackPlayer);
                }
                else if (WhitePlayer.PlayerId == Guid.Empty)
                {
                    WhitePlayer = new PlayerInfo(name);
                    return (BlackPlayer, WhitePlayer);
                }
                
                throw new HubException($"Failed to join game, game is full: {Id}");
            }

            public PlayerInfo GetPlayerInfo(Guid playerId) => GetPlayerInfo(GetPlayerColor(playerId));

            public PlayerInfo GetOtherPlayerInfo(Guid playerId) => GetPlayerInfo(GetPlayerColor(playerId).Opponent());

            public PlayerInfo GetPlayerInfo(PlayerColor player) =>
                player switch
                {
                    PlayerColor.Black => BlackPlayer,
                    PlayerColor.White => WhitePlayer,
                    _ => throw new HubException("unknown player")
                };

            public ClientGameInfo ToClientGameInfo() => ToClientGameInfo(Guid.Empty);

            // Convert the saved state of this game into information that the client can comsume
            public ClientGameInfo ToClientGameInfo(Guid requestingPlayerId) =>
                new ClientGameInfo()
                {
                    GameId = Id,
                    Created = Created,
                    LastPlayed = LastPlayed,
                    ClientColor = GetPlayerColor(requestingPlayerId).ToString(),
                    BlackName = BlackPlayer.PlayerName,
                    WhiteName = WhitePlayer.PlayerName,

                    // we don't generally want to send the client-ids off of the server to avoid
                    // leaking these (having someone else's client-id would allow spoofing) but
                    // we need to send back the requesting client's player-id so it can map back
                    // to its game request in the case where it has recorded both players in the
                    // game within the same client.
                    //  e.g. client requests game status as a set of (game-id, player-id) pairs,
                    //  so it might request both (3, 0) and (3, 1) we must send it back the
                    //  player-id so it can differentiate the results.
                    RequestingPlayerId = requestingPlayerId,
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
                    ["BlackPlayer_PlayerId"] = new EntityProperty(BlackPlayer.PlayerId),
                    ["BlackPlayer_PlayerName"] = new EntityProperty(BlackPlayer.PlayerName),
                    ["WhitePlayer_PlayerId"] = new EntityProperty(WhitePlayer.PlayerId),
                    ["WhitePlayer_PlayerName"] = new EntityProperty(WhitePlayer.PlayerName)
                };
            }

            public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
            {
                _game = TaikyokuShogi.Deserlialize(properties["Game"].BinaryValue);
                Id = properties["Id"].GuidValue ?? throw new Exception("Cannot deserialize");
                Created = properties["Created"].DateTime ?? throw new Exception("Cannot deserialize");
                LastPlayed = properties["LastPlayed"].DateTime ?? throw new Exception("Cannot deserialize");
                BlackPlayer.PlayerId = properties["BlackPlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
                BlackPlayer.PlayerName = properties["BlackPlayer_PlayerName"].StringValue;
                WhitePlayer.PlayerId = properties["WhitePlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
                WhitePlayer.PlayerName = properties["WhitePlayer_PlayerName"].StringValue;
            }

            string? ITableEntity.PartitionKey { get; set; }

            string? ITableEntity.RowKey { get; set; }

            string? ITableEntity.ETag { get; set; }

            DateTimeOffset ITableEntity.Timestamp { get; set; }
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

            logger.LogInformation($"adding client to game group");
            await Groups.AddToGroupAsync(context.ConnectionId, gameInfo.Id.ToString());

            return new GamePlayerPair(gameInfo.Id, asBlackPlayer ? gameInfo.BlackPlayer.PlayerId : gameInfo.WhitePlayer.PlayerId);
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
                            gameList.Add(gameInfo.ToClientGameInfo(request.RequestingPlayerId));
                    }));
            }

            return await Task.WhenAll(tasks.ToArray()).ContinueWith(t =>
            {
                return gameList as IEnumerable<ClientGameInfo>;
            });
        }

        [FunctionName(nameof(JoinGame))]
        public async Task JoinGame([SignalRTrigger] InvocationContext context, 
            Guid gameId, string playerName, ILogger logger)
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

            // join the group
            logger.LogInformation($"adding '{context.ConnectionId}' to '{gameId}' group");
            await Groups.AddToGroupAsync(context.ConnectionId, gameInfo.Id.ToString());

            // signal other player game has started
            logger.LogInformation($"signal player '{oldPlayerInfo}' game start for '{gameId}'");
            await Clients.GroupExcept(gameInfo.Id.ToString(), context.ConnectionId).
                ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.Id, oldPlayerInfo.PlayerId);

            // signal joining player game has started
            logger.LogInformation($"signal player '{newPlayerInfo}' game start for '{gameId}'");
            await Clients.Client(context.ConnectionId).
                ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.Id, newPlayerInfo.PlayerId);
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
            await Clients.Client(context.ConnectionId).ReceiveGameStart(game.ToJsonString(), Guid.NewGuid(), Guid.NewGuid());
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
        // database of games looking for players
        private static readonly ConcurrentDictionary<Guid, GameInfo> OpenGames = new ConcurrentDictionary<Guid, GameInfo>();

        // map players to their connections
        private static readonly ConcurrentDictionary<Guid, (IShogiClient Client, string ClientId)> ClientMap = new ConcurrentDictionary<Guid, (IShogiClient Client, string ClientId)>();
        private static readonly object ClientMapLock = new object();


        private GameInfo ClientGame { get => (GameInfo)Context.Items["ClientGame"]; set => Context.Items["ClientGame"] = value; }

        private Guid ClientPlayerId { get => Context.Items["ClientPlayerId"] as Guid? ?? Guid.Empty; set => Context.Items["ClientPlayerId"] = value; }

        public async Task RejoinGame(Guid gameId, Guid playerId)
        {
            // first disconnect from any games we're currently connected to
            await DisconnectClientGame();

            var gameInfo = await TableStorage.FindGame(gameId);
            ClientGame = gameInfo ?? throw new HubException($"Failed to join game; game id not found: {gameId}");
            ClientPlayerId = playerId;
            ClientMap[playerId] = (Clients.Caller, Context.ConnectionId);

            var requestedPlayer = gameInfo.GetPlayerColor(playerId);
            var otherPlayerInfo = gameInfo.GetPlayerInfo(requestedPlayer.Opponent());

            var otherClient = ClientMap.GetValueOrDefault(otherPlayerInfo.PlayerId).Client;
            if (otherClient != null)
                await otherClient.ReceiveGameReconnect(gameId);
            await Clients.Caller.ReceiveGameStart(gameInfo.Game, gameId, playerId, requestedPlayer, otherPlayerInfo.PlayerName);
        }

        public async Task CancelGame()
        {
            OpenGames.TryRemove(ClientGame.Id, out _);
            await Clients.All.ReceiveGameList(AllOpenGames());
        }

        // Record updated game state into persistant storage and notify any attached clients
        private async Task UpdateGame(GameInfo gameInfo)
        {
            gameInfo = TableStorage.AddOrUpdateGame(gameInfo)
                ?? throw new HubException("Interal Server error: cannot record move");
            ClientGame = gameInfo;

            var blackClient = ClientMap.GetValueOrDefault(gameInfo.BlackPlayer.PlayerId).Client;
            if (blackClient != null)
                await blackClient.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);

            var whiteClient = ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).Client;
            if (whiteClient != null)
                await whiteClient.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
        }

        public async Task RequestMove(Location startLoc, Location endLoc, Location midLoc, bool promote)
        {
            var gameInfo = ClientGame;
            var playerId = ClientPlayerId;

            if (gameInfo == null)
                throw new HubException("illegal move: no game in progress");

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

            await UpdateGame(gameInfo);
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