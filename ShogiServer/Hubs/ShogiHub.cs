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

        Task ReceiveGameStart(TaikyokuShogi game);
    }

    public class ShogiHub : Hub<IShogiClient>
    {
        private struct GameInfo
        {
            public readonly TaikyokuShogi Game;
            public readonly Guid Id;
            public readonly string Name;
            public readonly IShogiClient WhitePlayer;
            public readonly IShogiClient BlackPlayer;

            public bool Open() => WhitePlayer == null || BlackPlayer == null;

            public GameInfo(TaikyokuShogi game, Guid id, string name, IShogiClient blackPlayer, IShogiClient whitePlayer) =>
                (Game, Id, Name, BlackPlayer, WhitePlayer) = (game, id, name, blackPlayer, whitePlayer); 
        }

        // add some dummy games to test out the serivce
        static ShogiHub()
        {
            var id = Guid.NewGuid();
            Games[id] = new GameInfo(new TaikyokuShogi(TaikyokuShogiOptions.None), id, "test1", null, null);
            var id2 = Guid.NewGuid();
            Games[id2] = new GameInfo(new TaikyokuShogi(TaikyokuShogiOptions.None), id2, "test2", null, null);
        }

        // database of running games, key is "gameId" which is a GUID
        private static readonly ConcurrentDictionary<Guid, GameInfo> Games = new ConcurrentDictionary<Guid, GameInfo>();
        private static readonly object gameJoinLock = new object();

        private static List<NetworkGameInfo> GamesList
        {
            get => Games.Values.Where(info => info.Open()).Select(info => new NetworkGameInfo() { Name = info.Name, Id = info.Id }).ToList();
        }

        public Task CreateGame(string gameName, TaikyokuShogiOptions gameOptions, bool asBlackPlayer)
        {
            var game = new TaikyokuShogi(gameOptions);
            var gameId = Guid.NewGuid();
            var blackPlayer = asBlackPlayer ? Clients.Caller : null;
            var whitePlayer = asBlackPlayer ? null : Clients.Caller;
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
            lock (gameJoinLock)
            {
                if (!Games.TryGetValue(gameId, out var gameInfo))
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

                var blackPlayer = gameInfo.BlackPlayer ?? Clients.Caller;
                var whitePlayer = gameInfo.WhitePlayer ?? Clients.Caller;

                gameInfo = new GameInfo(gameInfo.Game, gameInfo.Id, gameInfo.Name, blackPlayer, whitePlayer);
                Games[gameId] = gameInfo;

                return Task.Run(() =>
                {
                    Clients.All.ReceiveGameList(GamesList);
                    blackPlayer.ReceiveGameStart(gameInfo.Game);
                    whitePlayer.ReceiveGameStart(gameInfo.Game);
                });
            }
        }

        #region ThrowHubException
        public Task ThrowException()
        {
            throw new HubException("This error will be sent to the client!");
        }
        #endregion

        #region OnConnectedAsync
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "SignalR Users");
            await base.OnConnectedAsync();
        }
        #endregion

        #region OnDisconnectedAsync
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "SignalR Users");
            await base.OnDisconnectedAsync(exception);
        }
        #endregion
    }
}