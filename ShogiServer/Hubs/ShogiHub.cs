using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.SignalR;

using ShogiEngine;

namespace ShogiServer.Hubs
{
    public interface IShogiClient
    {
        Task ReceiveNewGame(TaikyokuShogi game, Guid id);

        Task ReceiveGameList(List<GameListElement> list);
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

            public GameInfo(TaikyokuShogi game, Guid id, string name, IShogiClient blackPlayer, IShogiClient whitePlayer) =>
                (Game, Id, Name, BlackPlayer, WhitePlayer) = (game, id, name, blackPlayer, whitePlayer); 
        }

        // database of running games, key is "gameId" which is a GUID
        private static readonly ConcurrentDictionary<Guid, GameInfo> Games = new ConcurrentDictionary<Guid, GameInfo>();
        private static readonly object gameJoinLock = new object();

        public Task CreateGame(string gameName)
        {
            var game = new TaikyokuShogi();
            var gameId = Guid.NewGuid();
            GameInfo gameInfo = new GameInfo(game, gameId, gameName, Clients.Caller, null);
            Games[gameId] = gameInfo;
            return Clients.Caller.ReceiveNewGame(game, gameId);
        }

        public Task GetGames()
        {
            var list = Games.Values.Select(info => new GameListElement() { Name = info.Name, Id = info.Id }).ToList();
            return Clients.Caller.ReceiveGameList(list);
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

                return Clients.Caller.ReceiveNewGame(gameInfo.Game, gameId);
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