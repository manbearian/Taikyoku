using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

using ShogiEngine;
using ShogiComms;

namespace ShogiClient
{
    public class ReceiveGameUpdateEventArgs : EventArgs
    {
        public TaikyokuShogi Game { get; }

        public Guid GameId { get; }

        public ReceiveGameUpdateEventArgs(TaikyokuShogi game, Guid gameId) => (Game, GameId) = (game, gameId);
    }

    public class ReceiveGameStartEventArgs : EventArgs
    {
        public TaikyokuShogi Game { get; }

        public Guid GameId { get; }

        public Player Player { get; }

        public ReceiveGameStartEventArgs(TaikyokuShogi game, Guid gameId, Player player) => (Game, GameId, Player) = (game, gameId, player);
    }

    public class ReceiveGameConnectionEventArgs : EventArgs
    {
        public Guid GameId { get; }

        public ReceiveGameConnectionEventArgs(Guid gameId) => GameId = gameId;
    }

    public class ReceiveGameListEventArgs : EventArgs
    {
        public List<NetworkGameInfo> GameList { get; }

        public ReceiveGameListEventArgs(List<NetworkGameInfo> list) => GameList = list;
    }

    public class Connection
    {
        private readonly HubConnection _connection;

        public delegate void ReceiveNewGameHandler(object sender, ReceiveGameUpdateEventArgs e);
        public event ReceiveNewGameHandler OnReceiveNewGame;

        public delegate void ReceiveGameListHandler(object sender, ReceiveGameListEventArgs e);
        public event ReceiveGameListHandler OnReceiveGameList;

        public delegate void ReceiveGameStartHandler(object sender, ReceiveGameStartEventArgs e);
        public event ReceiveGameStartHandler OnReceiveGameStart;

        public delegate void ReceiveGameUpdateHandler(object sender, ReceiveGameUpdateEventArgs e);
        public event ReceiveGameUpdateHandler OnReceiveGameUpdate;

        public delegate void ReceiveGameDisconnectHandler(object sender, ReceiveGameConnectionEventArgs e);
        public event ReceiveGameDisconnectHandler OnReceiveGameDisconnect;

        public delegate void ReceiveGameReconnectHandler(object sender, ReceiveGameConnectionEventArgs e);
        public event ReceiveGameReconnectHandler OnReceiveGameReconnect;

        public Connection()
        {
            _connection = new HubConnectionBuilder().
                WithUrl("https://localhost:44352/ShogiHub").
                Build();

            _connection.Closed += async (error) =>
            {
                // manual reconnect on disconnect
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _connection.StartAsync();
            };

            _connection.On<TaikyokuShogi, Guid>("ReceiveNewGame", (gameObject, gameId) =>
                OnReceiveNewGame?.Invoke(this, new ReceiveGameUpdateEventArgs(gameObject, gameId)));

            _connection.On<List<NetworkGameInfo>>("ReceiveGameList", gameList =>
                OnReceiveGameList?.Invoke(this, new ReceiveGameListEventArgs(gameList)));

            _connection.On<TaikyokuShogi, Guid, Player>("ReceiveGameStart", (gameObject, gameId, player) =>
                OnReceiveGameStart?.Invoke(this, new ReceiveGameStartEventArgs(gameObject, gameId, player)));

            _connection.On<TaikyokuShogi, Guid>("ReceiveGameUpdate", (gameObject, gameId) =>
                OnReceiveGameUpdate?.Invoke(this, new ReceiveGameUpdateEventArgs(gameObject, gameId)));

            _connection.On<Guid>("ReceiveGameDisconnect", (gameId) =>
                OnReceiveGameDisconnect?.Invoke(this, new ReceiveGameConnectionEventArgs(gameId)));

            _connection.On<Guid>("ReceiveGameReconnect", (gameId) =>
                OnReceiveGameReconnect?.Invoke(this, new ReceiveGameConnectionEventArgs(gameId)));
        }

        public async Task ConnectAsync() =>
            await _connection.StartAsync();

        public async Task RequestGamesList() =>
            await _connection.InvokeAsync("GetGames");

        public async Task RequestNewGame(string gameName, TaikyokuShogiOptions gameOptions, bool asBlackPlayer) =>
            await _connection.InvokeAsync("CreateGame", gameName, gameOptions, asBlackPlayer);

        public async Task RequestJoinGame(Guid id) =>
            await _connection.InvokeAsync("JoinGame", id);

        public async Task RequestRejoinGame(Guid id, Player player) =>
            await _connection.InvokeAsync("RejoinGame", id, player);

        public async Task RequestCancelGame() =>
            await _connection.InvokeAsync("CancelGame");

        public async Task RequestMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc, bool promote) =>
            await _connection.InvokeAsync("MakeMove", (Location)startLoc, (Location)endLoc, (Location)midLoc, promote);
    }
}
