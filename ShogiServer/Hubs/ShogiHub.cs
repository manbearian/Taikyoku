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

        Task ReceiveGameStart(TaikyokuShogi game, Guid id, Player player);

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

            public string Name { get; }

            public (IShogiClient Client, string Id)? WhitePlayer { get; set; }

            public (IShogiClient Client, string Id)? BlackPlayer { get; set; }

            public GameInfo(TaikyokuShogi game, Guid id, string name) => (Game, Id, Name) = (game, id, name); 
        }

        // database of games looking for players
        private static readonly ConcurrentDictionary<Guid, GameInfo> OpenGames = new ConcurrentDictionary<Guid, GameInfo>();

        // database of running games, key is "gameId" which is a GUID
        private static readonly ConcurrentDictionary<Guid, GameInfo> RunningGames = new ConcurrentDictionary<Guid, GameInfo>();

        private static readonly object gameUpdateLock = new object();

        private static List<NetworkGameInfo> GamesList
        {
            get => OpenGames.Values.Select(info => new NetworkGameInfo() { Name = info.Name, Id = info.Id }).ToList();
        }

        private GameInfo ClientGame { get => (GameInfo)Context.Items["ClientGame"]; set => Context.Items["ClientGame"] = value; }

        public Task CreateGame(string gameName, TaikyokuShogiOptions gameOptions, bool asBlackPlayer)
        {
            var game = new TaikyokuShogi(gameOptions);
            var gameId = Guid.NewGuid();
            var blackPlayer = asBlackPlayer ? (Clients.Caller, Context.ConnectionId) : null as (IShogiClient Client, string Id)?;
            var whitePlayer = asBlackPlayer ? null as (IShogiClient Client, string Id)? : (Clients.Caller, Context.ConnectionId);
            var gameInfo = new GameInfo(game, gameId, gameName);

            if (asBlackPlayer)
                gameInfo.BlackPlayer = (Clients.Caller, Context.ConnectionId);
            else
                gameInfo.WhitePlayer = (Clients.Caller, Context.ConnectionId);

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

        public Task JoinGame(Guid gameId)
        {
            GameInfo gameInfo;

            lock (gameUpdateLock)
            {
                if (!OpenGames.TryRemove(gameId, out gameInfo))
                {
                    throw new HubException($"Failed to join game, game id not found: {gameId}");
                }

                if (gameInfo.WhitePlayer != null && gameInfo.BlackPlayer != null)
                {
                    throw new HubException($"Failed to join game, game is full: {gameId}");
                }

                if (gameInfo.WhitePlayer == null && gameInfo.BlackPlayer == null)
                {
                    throw new HubException($"Failed to join game, game is abandoned: {gameId}");
                }

                gameInfo.BlackPlayer ??= (Clients.Caller, Context.ConnectionId);
                gameInfo.WhitePlayer ??= (Clients.Caller, Context.ConnectionId);

                RunningGames[gameId] = gameInfo;
                ClientGame = gameInfo;
            }

            return Task.Run(() =>
            {
                Clients.All.ReceiveGameList(GamesList);
                gameInfo.BlackPlayer?.Client.ReceiveGameStart(gameInfo.Game, gameId, Player.Black);
                gameInfo.WhitePlayer?.Client.ReceiveGameStart(gameInfo.Game, gameId, Player.White);
            });
        }

        public Task RejoinGame(Guid gameId, Player requestedPlayer)
        {
            GameInfo gameInfo;
            IShogiClient otherClient = null;

            lock (gameUpdateLock)
            {
                if (!RunningGames.TryGetValue(gameId, out gameInfo))
                {
                    throw new HubException($"Failed to join game, game id not found: {gameId}");
                }

                if (requestedPlayer == Player.Black)
                {
                    if (gameInfo.BlackPlayer != null)
                        throw new HubException($"Failed to join game, game is full: {gameId}");

                    gameInfo.BlackPlayer = (Clients.Caller, Context.ConnectionId);
                    otherClient = gameInfo.WhitePlayer?.Client;
                }
                else if (requestedPlayer == Player.White)
                {
                    if (gameInfo.WhitePlayer != null)
                        throw new HubException($"Failed to join game, game is full: {gameId}");

                    gameInfo.WhitePlayer = (Clients.Caller, Context.ConnectionId);
                    otherClient = gameInfo.BlackPlayer?.Client;
                }

                ClientGame = gameInfo;
            }


            return Task.Run(() =>
            {
                otherClient?.ReceiveGameReconnect(gameId);
                Clients.Caller.ReceiveGameStart(gameInfo.Game, gameId, requestedPlayer);
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
            if (gameInfo.BlackPlayer?.Id == Context.ConnectionId)
                requestingPlayer = Player.Black;
            else if (gameInfo.WhitePlayer?.Id == Context.ConnectionId)
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
                gameInfo.BlackPlayer?.Client.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
                gameInfo.WhitePlayer?.Client.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
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
                    if (gameInfo.BlackPlayer?.Id == Context.ConnectionId)
                    {
                        otherClient = gameInfo.WhitePlayer?.Client;
                        gameInfo.BlackPlayer = null;
                    }
                    else if (gameInfo.WhitePlayer?.Id == Context.ConnectionId)
                    {
                        otherClient = gameInfo.BlackPlayer?.Client;
                        gameInfo.WhitePlayer = null;
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