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

        Task ReceiveGameStart(TaikyokuShogi game, Guid id);

        Task ReceiveGameCancel(TaikyokuShogi game, Guid id);

        Task ReceiveGameUpdate(TaikyokuShogi game, Guid id);
    }

    public class ShogiHub : Hub<IShogiClient>
    {
        private struct GameInfo
        {
            public readonly TaikyokuShogi Game;
            public readonly Guid Id;
            public readonly string Name;
            public readonly (IShogiClient Client, string Id) WhitePlayer;
            public readonly (IShogiClient Client, string Id) BlackPlayer;

            public bool Open() => WhitePlayer == default|| BlackPlayer == default;

            public GameInfo(TaikyokuShogi game, Guid id, string name, (IShogiClient Client, string Id) blackPlayer, (IShogiClient Client, string Id) whitePlayer) =>
                (Game, Id, Name, BlackPlayer, WhitePlayer) = (game, id, name, blackPlayer, whitePlayer); 
        }

        // add some dummy games to test out the serivce
        static ShogiHub()
        {
            var id = Guid.NewGuid();
            Games[id] = new GameInfo(new TaikyokuShogi(TaikyokuShogiOptions.None), id, "test1", default, default);
            var id2 = Guid.NewGuid();
            Games[id2] = new GameInfo(new TaikyokuShogi(TaikyokuShogiOptions.None), id2, "test2", default, default);
        }

        // database of running games, key is "gameId" which is a GUID
        private static readonly ConcurrentDictionary<Guid, GameInfo> Games = new ConcurrentDictionary<Guid, GameInfo>();
        private static readonly object gameUpdateLock = new object();

        private static List<NetworkGameInfo> GamesList
        {
            get => Games.Values.Where(info => info.Open()).Select(info => new NetworkGameInfo() { Name = info.Name, Id = info.Id }).ToList();
        }

        private GameInfo ClientGame
        {
            get => Games.Values.Where(g => g.WhitePlayer.Id == Context.ConnectionId || g.BlackPlayer.Id == Context.ConnectionId).SingleOrDefault();
        }

        public Task CreateGame(string gameName, TaikyokuShogiOptions gameOptions, bool asBlackPlayer)
        {
            var game = new TaikyokuShogi(gameOptions);
            var gameId = Guid.NewGuid();
            var blackPlayer = asBlackPlayer ? (Clients.Caller, Context.ConnectionId) : default;
            var whitePlayer = asBlackPlayer ? default : (Clients.Caller, Context.ConnectionId);
            GameInfo gameInfo = new GameInfo(game, gameId, gameName, blackPlayer, whitePlayer);
            Games[gameId] = gameInfo;

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
                if (!Games.TryGetValue(gameId, out gameInfo))
                {
                    throw new HubException($"Failed to join game, game id not found: {gameId}");
                }
                
                if (gameInfo.WhitePlayer != default && gameInfo.BlackPlayer != default)
                {
                    throw new HubException($"Failed to join game, game is full: {gameId}");
                }

                if (gameInfo.WhitePlayer == default && gameInfo.BlackPlayer == default)
                {
                    throw new HubException($"Failed to join game, game is abandoned: {gameId}");
                }

                var blackPlayer = gameInfo.BlackPlayer == default ? (Clients.Caller, Context.ConnectionId) : gameInfo.BlackPlayer;
                var whitePlayer = gameInfo.WhitePlayer == default ? (Clients.Caller, Context.ConnectionId) : gameInfo.WhitePlayer;

                gameInfo = new GameInfo(gameInfo.Game, gameInfo.Id, gameInfo.Name, blackPlayer, whitePlayer);
                Games[gameId] = gameInfo;
            }

            return Task.Run(() =>
            {
                Clients.All.ReceiveGameList(GamesList);
                gameInfo.BlackPlayer.Client.ReceiveGameStart(gameInfo.Game, gameInfo.Id);
                gameInfo.WhitePlayer.Client.ReceiveGameStart(gameInfo.Game, gameInfo.Id);
            });
        }

        public Task CancelGame()
        {
            GameInfo gameInfo = ClientGame;
            bool removed = false;

            lock (gameUpdateLock)
            {
                if (gameInfo.BlackPlayer != default && gameInfo.WhitePlayer != default)
                    throw new HubException("game already in progres, cannot cancel");

                removed = Games.TryRemove(gameInfo.Id, out gameInfo);
            }

            return Task.Run(() =>
            {
                if (removed)
                {
                    Clients.All.ReceiveGameList(GamesList);
                    gameInfo.BlackPlayer.Client?.ReceiveGameCancel(gameInfo.Game, gameInfo.Id);
                    gameInfo.WhitePlayer.Client?.ReceiveGameCancel(gameInfo.Game, gameInfo.Id);
                }
            });
        }

        public Task MakeMove(Location startLoc, Location endLoc, Location midLoc, bool promote)
        {
            var gameInfo = ClientGame;
            Player? requestingPlayer = null;
            if (gameInfo.BlackPlayer.Id == Context.ConnectionId)
                requestingPlayer = Player.Black;
            else if (gameInfo.WhitePlayer.Id == Context.ConnectionId)
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
                gameInfo.BlackPlayer.Client.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
                gameInfo.WhitePlayer.Client.ReceiveGameUpdate(gameInfo.Game, gameInfo.Id);
            });
        }
    }
}