using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.SignalR;

using ShogiEngine;
using ShogiComms;

namespace ShogiServer.Hubs
{
    public interface IShogiClient
    {
        Task ReceiveNewGame(TaikyokuShogi game, Guid id);

        Task ReceiveGameList(List<NetworkGameInfo> list);

        Task ReceiveGameStart(TaikyokuShogi game, Guid id, Guid playerId, Player player);

        Task ReceiveGameUpdate(TaikyokuShogi game, Guid id);

        // indicate the other player disconncted
        Task ReceiveGameDisconnect(Guid id);

        // indicate that the other player reconnected
        Task ReceiveGameReconnect(Guid id);
    }

    public class ShogiHub : Hub<IShogiClient>
    {
        private class GameInfo
        {
            public TaikyokuShogi Game { get; }

            public Guid Id { get; }

            public DateTime Created { get; }

            public (IShogiClient Client, string ClientId, Guid PlayerId, string PlayerName) WhitePlayer { get; set; }

            public (IShogiClient Client, string ClientId, Guid PlayerId, string PlayerName) BlackPlayer { get; set; }

            public GameInfo(TaikyokuShogi game, Guid id) => (Game, Id, Created) = (game, id, DateTime.UtcNow); 
        }

        // database of games looking for players
        private static readonly ConcurrentDictionary<Guid, GameInfo> OpenGames = new ConcurrentDictionary<Guid, GameInfo>();

        // database of running games, key is "gameId" which is a GUID
        private static readonly ConcurrentDictionary<Guid, GameInfo> RunningGames = new ConcurrentDictionary<Guid, GameInfo>();

        private static readonly object gameUpdateLock = new object();

        private static List<NetworkGameInfo> GamesList
        {
            get => OpenGames.Values.Select(info => new NetworkGameInfo()
            {
                GameId = info.Id,
                Created = info.Created,
                BlackName = info.BlackPlayer.PlayerName,
                WhiteName = info.WhitePlayer.PlayerName,
            }).ToList();
        }

        private GameInfo ClientGame { get => (GameInfo)Context.Items["ClientGame"]; set => Context.Items["ClientGame"] = value; }

        public Task CreateGame(string playerName, TaikyokuShogiOptions gameOptions, bool asBlackPlayer, TaikyokuShogi existingGame)
        {
            var game = existingGame ?? new TaikyokuShogi(gameOptions);
            var gameId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var blackPlayer = asBlackPlayer ? (Clients.Caller, Context.ConnectionId) : null as (IShogiClient Client, string Id)?;
            var whitePlayer = asBlackPlayer ? null as (IShogiClient Client, string Id)? : (Clients.Caller, Context.ConnectionId);
            var gameInfo = new GameInfo(game, gameId);

            if (asBlackPlayer)
                gameInfo.BlackPlayer = (Clients.Caller, Context.ConnectionId, playerId, playerName);
            else
                gameInfo.WhitePlayer = (Clients.Caller, Context.ConnectionId, playerId, playerName);

            OpenGames[gameId] = gameInfo;
            ClientGame = gameInfo;

            return Task.Run(() =>
            {
                Clients.All.ReceiveGameList(GamesList);
                Clients.Caller.ReceiveNewGame(game, gameId);
            });
        }

        public Task GetGames()
        {
            return Clients.Caller.ReceiveGameList(GamesList);
        }

        public Task JoinGame(Guid gameId, string playerName)
        {
            GameInfo gameInfo;

            lock (gameUpdateLock)
            {
                if (!OpenGames.TryRemove(gameId, out gameInfo))
                {
                    throw new HubException($"Failed to join game, game id not found: {gameId}");
                }

                var playerId = Guid.NewGuid();

                if (gameInfo.BlackPlayer.Client == null)
                {
                    if (gameInfo.WhitePlayer.Client == null)
                    {
                        throw new HubException($"Failed to join game, game is abandoned: {gameId}");
                    }

                    gameInfo.BlackPlayer = (Clients.Caller, Context.ConnectionId, playerId, playerName);
                }
                else if (gameInfo.WhitePlayer.Client == null)
                {
                    gameInfo.WhitePlayer = (Clients.Caller, Context.ConnectionId, playerId, playerName);
                }
                else
                {
                    throw new HubException($"Failed to join game, game is full: {gameId}");
                }

                RunningGames[gameId] = gameInfo;
                ClientGame = gameInfo;
            }

            return Task.Run(() =>
            {
                Clients.All.ReceiveGameList(GamesList);
                gameInfo.BlackPlayer.Client.ReceiveGameStart(gameInfo.Game, gameId, gameInfo.BlackPlayer.PlayerId, Player.Black);
                gameInfo.WhitePlayer.Client.ReceiveGameStart(gameInfo.Game, gameId, gameInfo.WhitePlayer.PlayerId, Player.White);
            });
        }

        public Task RejoinGame(Guid gameId, Guid playerId)
        {
            GameInfo gameInfo;
            Player requestedPlayer;
            IShogiClient otherClient = null;

            lock (gameUpdateLock)
            {
                if (!RunningGames.TryGetValue(gameId, out gameInfo))
                {
                    throw new HubException($"Failed to join game, game id not found: {gameId}");
                }

                if (playerId == gameInfo.BlackPlayer.PlayerId)
                {
                    if (gameInfo.BlackPlayer.Client != null)
                        throw new HubException($"Failed to join game, game is full: {gameId}");

                    gameInfo.BlackPlayer = (Clients.Caller, Context.ConnectionId, playerId, gameInfo.BlackPlayer.PlayerName);
                    requestedPlayer = Player.Black;
                    otherClient = gameInfo.WhitePlayer.Client;
                }
                else if (playerId == gameInfo.WhitePlayer.PlayerId)
                {
                    if (gameInfo.WhitePlayer.Client != null)
                        throw new HubException($"Failed to join game, game is full: {gameId}");

                    gameInfo.WhitePlayer = (Clients.Caller, Context.ConnectionId, playerId, gameInfo.WhitePlayer.PlayerName);
                    requestedPlayer = Player.White;
                    otherClient = gameInfo.BlackPlayer.Client;
                }
                else
                {
                    throw new HubException($"Failed to join game, player id not found: {playerId}");
                }

                ClientGame = gameInfo;
            }


            return Task.Run(() =>
            {
                otherClient?.ReceiveGameReconnect(gameId);
                Clients.Caller.ReceiveGameStart(gameInfo.Game, gameId, playerId, requestedPlayer);
            });
        }

        public Task CancelGame()
        {
            GameInfo gameInfo = ClientGame;

            lock (gameUpdateLock)
            {
                if (RunningGames.ContainsKey(gameInfo.Id))
                    throw new HubException("game already in progress, cannot cancel");

                OpenGames.TryRemove(gameInfo.Id, out gameInfo);
            }

            return Clients.All.ReceiveGameList(GamesList);
        }

        public Task MakeMove(Location startLoc, Location endLoc, Location midLoc, bool promote)
        {
            var gameInfo = ClientGame;
            Player? requestingPlayer = null;
            if (gameInfo.BlackPlayer.ClientId == Context.ConnectionId)
                requestingPlayer = Player.Black;
            else if (gameInfo.WhitePlayer.ClientId == Context.ConnectionId)
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

            return Task.Run(() =>
            {
                gameInfo.BlackPlayer.Client?.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
                gameInfo.WhitePlayer.Client?.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
            });
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            var cleanupTask = base.OnDisconnectedAsync(exception);

            GameInfo gameInfo = ClientGame;
            if (gameInfo != null)
            {
                if (OpenGames.ContainsKey(gameInfo.Id))
                {
                    cleanupTask = CancelGame().ContinueWith(_ => cleanupTask);
                }
                else
                {
                    IShogiClient otherClient = null;
                    if (gameInfo.BlackPlayer.ClientId == Context.ConnectionId)
                    {
                        otherClient = gameInfo.WhitePlayer.Client;
                        gameInfo.BlackPlayer = (null, null, gameInfo.BlackPlayer.PlayerId, gameInfo.BlackPlayer.PlayerName);
                    }
                    else if (gameInfo.WhitePlayer.ClientId == Context.ConnectionId)
                    {
                        otherClient = gameInfo.BlackPlayer.Client;
                        gameInfo.WhitePlayer = (null, null, gameInfo.WhitePlayer.PlayerId, gameInfo.WhitePlayer.PlayerName);
                    }
                    else
                    {
                        throw new Exception("Unexpected client disconnection");
                    }

                    if (otherClient != null)
                    {
                        cleanupTask = otherClient.ReceiveGameDisconnect(gameInfo.Id).ContinueWith(_ => cleanupTask);
                    }
                }
            }

            return cleanupTask;
        }
    }
}