﻿using System;
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

        public delegate void ReceiveGameStartHandler(object sender, ReceiveGameUpdateEventArgs e);
        public event ReceiveGameStartHandler OnReceiveGameStart;

        public delegate void ReceiveGameCancelHandler(object sender, ReceiveGameUpdateEventArgs e);
        public event ReceiveGameCancelHandler OnReceiveGameCancel;

        public Connection()
        {
            _connection = new HubConnectionBuilder().
                WithUrl("https://localhost:44352/ShogiHub").
                Build();

            _connection.Closed += async (error) =>
            {
                // manual reconnect on disco    nnect
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _connection.StartAsync();
            };

            _connection.On<TaikyokuShogi, Guid>("ReceiveNewGame", (gameObject, gameId) =>
                OnReceiveNewGame?.Invoke(this, new ReceiveGameUpdateEventArgs(gameObject, gameId)));

            _connection.On<List<NetworkGameInfo>>("ReceiveGameList", gameList =>
                OnReceiveGameList?.Invoke(this, new ReceiveGameListEventArgs(gameList)));

            _connection.On<TaikyokuShogi, Guid>("ReceiveGameStart", (gameObject, gameId) =>
                OnReceiveGameStart?.Invoke(this, new ReceiveGameUpdateEventArgs(gameObject, gameId)));

            _connection.On<TaikyokuShogi, Guid>("ReceiveGameCancel", (gameObject, gameId) =>
                OnReceiveGameCancel?.Invoke(this, new ReceiveGameUpdateEventArgs(gameObject, gameId)));
        }

        public async Task ConnectAsync() =>
            await _connection.StartAsync();

        public async Task RequestGamesList() =>
            await _connection.InvokeAsync("GetGames");

        public async Task RequestNewGame(string gameName, TaikyokuShogiOptions gameOptions, bool asBlackPlayer) =>
            await _connection.InvokeAsync("CreateGame", gameName, gameOptions, asBlackPlayer);

        public async Task RequestJoinGame(Guid id) =>
            await _connection.InvokeAsync("JoinGame", id);

        public async Task RequestCancelGame(Guid id) =>
            await _connection.InvokeAsync("CancelGame", id);
    }
}
