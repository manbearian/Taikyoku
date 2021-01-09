using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Azure;

using Azure.Storage.Queues;
using Azure.Storage.Blobs;
using Azure.Core.Extensions;

using ShogiEngine;
using ShogiComms;

namespace ShogiServer.Hubs
{

    public interface IShogiClient
    {
        Task ReceiveNewGame(TaikyokuShogi game, Guid id);

        Task ReceiveGameList(List<ClientGameInfo> list);

        Task ReceiveGameStart(TaikyokuShogi game, Guid id, Guid playerId, Player player);

        Task ReceiveGameUpdate(TaikyokuShogi game, Guid id);

        // indicate the other player disconncted
        Task ReceiveGameDisconnect(Guid id);

        // indicate that the other player reconnected
        Task ReceiveGameReconnect(Guid id);
    }

    public class ShogiHub : Hub<IShogiClient>
    {
        internal class GameInfo : ITableEntity
        {
            internal class PlayerInfo
            {
                public Guid PlayerId { get; set; }

                public string PlayerName { get; set; }
            }

            public TaikyokuShogi Game { get; private set; }

            public Guid Id { get; private set; }

            public DateTime Created { get; private set; }

            public DateTime LastPlayed { get; set; }

            public PlayerInfo BlackPlayer { get; } = new PlayerInfo();

            public PlayerInfo WhitePlayer { get; } = new PlayerInfo();

            public GameInfo(TaikyokuShogi game, Guid id)
            {
                (Game, Id, Created, LastPlayed) = (game, id, DateTime.UtcNow, DateTime.Now);
                (((ITableEntity)this).PartitionKey, ((ITableEntity)this).RowKey) = (string.Empty, id.ToString());
            }

            public Player GetPlayerColor(Guid playerId) => 
                playerId == BlackPlayer.PlayerId ? Player.Black :
                    (playerId == WhitePlayer.PlayerId ? Player.White :
                        throw new HubException("unknown player"));

            public PlayerInfo GetPlayerInfo(Guid playerId) => GetPlayerInfo(GetPlayerColor(playerId));

            public PlayerInfo GetOtherPlayerInfo(Guid playerId) => GetPlayerInfo(GetPlayerColor(playerId).Opponent());

            public PlayerInfo GetPlayerInfo(Player player) =>
                player switch
                {
                    Player.Black => BlackPlayer,
                    Player.White => WhitePlayer,
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
                    // to its  game request in the case where it has recorded both players in the
                    // game within the same client.
                    //  e.g. client requests game status as a set of (game-id, player-id) pairs,
                    //  so it might request both (3, 0) and (3, 1) we must send it back the
                    //  player-id  so it can differentiate the results.
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
                Game = TaikyokuShogi.Deserlialize(properties["Game"].BinaryValue);
                Id = properties["Id"].GuidValue ?? throw new Exception("Cannot deserialize");
                Created = properties["Created"].DateTime ?? throw new Exception("Cannot deserialize");
                LastPlayed = properties["LastPlayed"].DateTime ?? throw new Exception("Cannot deserialize");
                BlackPlayer.PlayerId = properties["BlackPlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
                BlackPlayer.PlayerName = properties["BlackPlayer_PlayerName"].StringValue;
                WhitePlayer.PlayerId = properties["WhitePlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
                WhitePlayer.PlayerName = properties["WhitePlayer_PlayerName"].StringValue;
            }

            string ITableEntity.PartitionKey { get; set; }

            string ITableEntity.RowKey { get; set; }

            string ITableEntity.ETag { get; set; }

            DateTimeOffset ITableEntity.Timestamp { get; set; }
        }

        // database of games looking for players
        private static readonly ConcurrentDictionary<Guid, GameInfo> OpenGames = new ConcurrentDictionary<Guid, GameInfo>();

        // map players to their connections
        private static readonly ConcurrentDictionary<Guid, (IShogiClient Client, string ClientId)> ClientMap = new ConcurrentDictionary<Guid, (IShogiClient Client, string ClientId)>();
        private static readonly object ClientMapLock = new object();

        private GameInfo ClientGame { get => (GameInfo)Context.Items["ClientGame"]; set => Context.Items["ClientGame"] = value; }

        private Guid ClientPlayerId { get => Context.Items["ClientPlayerId"] as Guid? ?? Guid.Empty; set => Context.Items["ClientPlayerId"] = value; }

        private static List<ClientGameInfo> AllOpenGames()
             => OpenGames.Values.Select(info => info.ToClientGameInfo()).ToList();

        public async Task CreateGame(string playerName, TaikyokuShogiOptions gameOptions, bool asBlackPlayer, TaikyokuShogi existingGame)
        {
            var game = existingGame ?? new TaikyokuShogi(gameOptions);
            var gameId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var gameInfo = new GameInfo(game, gameId);

            var playerInfo = asBlackPlayer ? gameInfo.BlackPlayer : gameInfo.WhitePlayer;
            (playerInfo.PlayerId, playerInfo.PlayerName) = (playerId, playerName);

            ClientMap[playerId] = (Clients.Caller, Context.ConnectionId);
            OpenGames[gameId] = gameInfo;
            ClientGame = gameInfo;
            ClientPlayerId = playerId;

            await Clients.All.ReceiveGameList(AllOpenGames());
            await Clients.Caller.ReceiveNewGame(game, gameId);
        }

        public async Task RequestAllOpenGameInfo()
        {
            await Clients.Caller.ReceiveGameList(AllOpenGames());
        }

        public async Task RequestGameInfo(IEnumerable<NetworkGameRequest> requests)
        {
            var gameList = new ConcurrentBag<ClientGameInfo>();

            // query the table storage for all the requested games
            var tasks = new List<Task>();
            foreach (var request in requests)
            {
                tasks.Add(Program.TableStorage.FindGame(request.GameId).
                    ContinueWith(t =>
                    {
                        var gameInfo = t.Result;
                        if (gameInfo != null)
                            gameList.Add(gameInfo.ToClientGameInfo(request.RequestingPlayerId));
                    }));
            }

            Task.WaitAll(tasks.ToArray());
            await Clients.Caller.ReceiveGameList(gameList.ToList());
        }

        public async Task JoinGame(Guid gameId, string playerName)
        {
            // first disconnect from any games we're currently connected to
            await DisconnectClientGame();

            if (!OpenGames.TryRemove(gameId, out var gameInfo))
            {
                throw new HubException($"Failed to join game, game id not found: {gameId}");
            }

            GameInfo.PlayerInfo clientInfo;

            if (gameInfo.BlackPlayer.PlayerId == Guid.Empty)
            {
                if (gameInfo.WhitePlayer.PlayerId == Guid.Empty)
                {
                    throw new HubException($"Failed to join game, game is abandoned: {gameId}");
                }

                clientInfo = gameInfo.BlackPlayer;
            }
            else if (gameInfo.WhitePlayer.PlayerId == Guid.Empty)
            {
                clientInfo = gameInfo.WhitePlayer;
            }
            else
            {
                throw new HubException($"Failed to join game, game is full: {gameId}");
            }

            var playerId = Guid.NewGuid();
            (clientInfo.PlayerId, clientInfo.PlayerName) = (playerId, playerName);
            ClientMap[playerId] = (Clients.Caller, Context.ConnectionId);
            ClientPlayerId = playerId;
            ClientGame = gameInfo;

            await Program.TableStorage.AddGame(gameInfo);
            await Clients.All.ReceiveGameList(AllOpenGames());

            var blackClient = ClientMap.GetValueOrDefault(gameInfo.BlackPlayer.PlayerId).Client;
            if (blackClient != null)
                await blackClient.ReceiveGameStart(gameInfo.Game, gameId, gameInfo.BlackPlayer.PlayerId, Player.Black);

            var whiteClient = ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).Client;
            if (whiteClient != null)
                await whiteClient.ReceiveGameStart(gameInfo.Game, gameId, gameInfo.WhitePlayer.PlayerId, Player.White);
        }

        public async Task RejoinGame(Guid gameId, Guid playerId)
        {
            // first disconnect from any games we're currently connected to
            await DisconnectClientGame();

            var gameInfo = await Program.TableStorage.FindGame(gameId);
            ClientGame = gameInfo;
            ClientPlayerId = playerId;
            ClientMap[playerId] = (Clients.Caller, Context.ConnectionId);

            if (gameInfo == null)
            {
                throw new HubException($"Failed to join game, game id not found: {gameId}");
            }

            var requestedPlayer = gameInfo.GetPlayerColor(playerId);
            var otherPlayerInfo = gameInfo.GetPlayerInfo(requestedPlayer.Opponent());

            var otherClient = ClientMap.GetValueOrDefault(otherPlayerInfo.PlayerId).Client;
            if (otherClient != null)
                await otherClient.ReceiveGameReconnect(gameId);
            await Clients.Caller.ReceiveGameStart(gameInfo.Game, gameId, playerId, requestedPlayer);
        }

        public async Task CancelGame()
        {
            OpenGames.TryRemove(ClientGame.Id, out _);
            await Clients.All.ReceiveGameList(AllOpenGames());
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

            // Record updated game state into persistant storage
            // TODO: there maybe a a reace here where the next move could commit before this move
            //       somewhere we need to ensure that only the latest game update is saved to the DB
            //       to avoid appearing to travel backwards in time.
            await Program.TableStorage.AddGame(gameInfo);

            var blackClient = ClientMap.GetValueOrDefault(gameInfo.BlackPlayer.PlayerId).Client;
            if (blackClient != null)
                await blackClient.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);

            var whiteClient = ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).Client;
            if (whiteClient != null)
                await whiteClient.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
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
    }
}