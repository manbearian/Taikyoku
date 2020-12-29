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

        Task ReceiveGameList(IEnumerable<NetworkGameInfo> list);

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

            public PlayerInfo BlackPlayer { get; } = new PlayerInfo();

            public PlayerInfo WhitePlayer { get; } = new PlayerInfo();

            public GameInfo(TaikyokuShogi game, Guid id)
            {
                (Game, Id, Created) = (game, id, DateTime.UtcNow);
                (((ITableEntity)this).PartitionKey, ((ITableEntity)this).RowKey) = (string.Empty, id.ToString());
            }

            public NetworkGameInfo ToNetworkGameInfo() =>
                new NetworkGameInfo()
                {
                    GameId = Id,
                    Created = Created,
                    BlackName = BlackPlayer.PlayerName,
                    WhiteName = WhitePlayer.PlayerName,
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

        private static IEnumerable<NetworkGameInfo> GamesList
        {
            get => OpenGames.Values.Select(info => info.ToNetworkGameInfo());
        }

        private GameInfo ClientGame { get => (GameInfo)Context.Items["ClientGame"]; set => Context.Items["ClientGame"] = value; }

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

            await Clients.All.ReceiveGameList(GamesList);
            await Clients.Caller.ReceiveNewGame(game, gameId);
        }

        public async Task RequestAllOpenGameInfo()
        {
            await Clients.Caller.ReceiveGameList(GamesList);
        }

        public async Task RequestGameInfo(IEnumerable<NetworkGameRequest> requests)
        {
            var gamelist = new ConcurrentBag<NetworkGameInfo>();

            // query the table storage for all the requested games
            var tasks = new List<Task>();
            foreach (var request in requests)
            {
                tasks.Add(Program.TableStorage.FindGame(request.GameId).
                    ContinueWith(t =>
                    {
                        var gameInfo = t.Result;
                        if (request.RequestingPlayerId != gameInfo.BlackPlayer.PlayerId && request.RequestingPlayerId != gameInfo.WhitePlayer.PlayerId)
                            throw new HubException("No permission to request game information");
                        gamelist.Add(gameInfo.ToNetworkGameInfo());
                    }));
            }

            Task.WaitAll(tasks.ToArray());
            await Clients.Caller.ReceiveGameList(gamelist);
        }

        public async Task JoinGame(Guid gameId, string playerName)
        {
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
            ClientGame = gameInfo;

            await Program.TableStorage.AddGame(gameInfo);
            await Clients.All.ReceiveGameList(GamesList);

            var blackClient = ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).Client;
            if (blackClient != null)
                await blackClient.ReceiveGameStart(gameInfo.Game, gameId, gameInfo.BlackPlayer.PlayerId, Player.Black);

            var whiteClient = ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).Client;
            if (whiteClient != null)
                await whiteClient.ReceiveGameStart(gameInfo.Game, gameId, gameInfo.WhitePlayer.PlayerId, Player.White);
        }

        public async Task RejoinGame(Guid gameId, Guid playerId)
        {
            var gameInfo = await Program.TableStorage.FindGame(gameId);
            ClientGame = gameInfo;

            if (gameInfo == null)
            {
                throw new HubException($"Failed to join game, game id not found: {gameId}");
            }

            Player requestedPlayer;
            GameInfo.PlayerInfo playerInfo;
            GameInfo.PlayerInfo otherPlayerInfo;

            if (playerId == gameInfo.BlackPlayer.PlayerId)
            {
                requestedPlayer = Player.Black;
                playerInfo = gameInfo.BlackPlayer;
                otherPlayerInfo = gameInfo.WhitePlayer;
            }
            else if (playerId == gameInfo.WhitePlayer.PlayerId)
            {
                requestedPlayer = Player.White;
                playerInfo = gameInfo.WhitePlayer;
                otherPlayerInfo = gameInfo.BlackPlayer;
            }
            else
            {
                throw new HubException($"Failed to join game, player id not found: {playerId}");
            }

            if (ClientMap.TryGetValue(playerInfo.PlayerId, out _))
                throw new HubException($"Failed to join game, game is full: {gameId}");

            ClientMap[playerInfo.PlayerId] = (Clients.Caller, Context.ConnectionId);

            var otherClient = ClientMap.GetValueOrDefault(otherPlayerInfo.PlayerId).Client;
            if (otherClient != null)
                await otherClient.ReceiveGameReconnect(gameId);
            await Clients.Caller.ReceiveGameStart(gameInfo.Game, gameId, playerId, requestedPlayer);
        }

        public async Task CancelGame()
        {
            OpenGames.TryRemove(ClientGame.Id, out _);

            await Clients.All.ReceiveGameList(GamesList);
        }

        public async Task MakeMove(Location startLoc, Location endLoc, Location midLoc, bool promote)
        {
            var gameInfo = ClientGame;

            if (gameInfo == null)
                throw new HubException("illegal move: no game in progress");

            Player? requestingPlayer = null;
            if (ClientMap.GetValueOrDefault(gameInfo.BlackPlayer.PlayerId).ClientId == Context.ConnectionId)
                requestingPlayer = Player.Black;
            else if (ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).ClientId == Context.ConnectionId)
                requestingPlayer = Player.White;

            if (requestingPlayer == null || gameInfo.Game.CurrentPlayer != requestingPlayer)
                throw new HubException("illegal move: wrong client requested the move");

            try
            {
                gameInfo.Game.MakeMove(((int, int))startLoc, ((int, int))endLoc, ((int, int)?)midLoc, promote);
            }
            catch (InvalidOperationException e)
            {
                throw new HubException("invalid move: unable to complete move", e);
            }


            var blackClient = ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).Client;
            if (blackClient != null)
                await blackClient.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);

            var whiteClient = ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).Client;
            if (whiteClient != null)
                await whiteClient.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);

            GameInfo gameInfo = ClientGame;

            if (gameInfo == null)
                return;

            GameInfo.PlayerInfo playerInfo;
            GameInfo.PlayerInfo otherPlayerInfo;

            if (OpenGames.ContainsKey(gameInfo.Id))
            {
                // if the game is pending (no opponent connected yet) remove it from list of pending games
                await CancelGame();
                return;
            }

            // remove our client information (so it no longer gets game updates)
            // signal the other client (if present) that there was a disconnect

            if (ClientMap.GetValueOrDefault(gameInfo.BlackPlayer.PlayerId).ClientId == Context.ConnectionId)
            {
                playerInfo = gameInfo.BlackPlayer;
                otherPlayerInfo = gameInfo.WhitePlayer;
            }
            else if (ClientMap.GetValueOrDefault(gameInfo.WhitePlayer.PlayerId).ClientId == Context.ConnectionId)
            {
                playerInfo = gameInfo.WhitePlayer;
                otherPlayerInfo = gameInfo.BlackPlayer;
            }
            else
            {
                throw new Exception("Unexpected client disconnection");
            }

            ClientMap.TryRemove(playerInfo.PlayerId, out _);

            var otherClient = ClientMap.GetValueOrDefault(otherPlayerInfo.PlayerId).Client;
            if (otherClient != null)
                await otherClient.ReceiveGameDisconnect(gameInfo.Id);
        }
    }
}