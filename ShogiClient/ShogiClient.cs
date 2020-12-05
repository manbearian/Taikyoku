using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

using ShogiEngine;
using ShogiComms;

namespace ShogiClient
{
    public class ReceiveNewGameEventArgs : EventArgs
    {
        public TaikyokuShogi Game { get; }

        public Guid Id { get; }

        public ReceiveNewGameEventArgs(TaikyokuShogi game, Guid id) => (Game, Id) = (game, id);
    }

    public class ReceiveGameListEventArgs : EventArgs
    {
        public List<NetworkGameInfo> GameList { get; }

        public ReceiveGameListEventArgs(List<NetworkGameInfo> list) => GameList = list;
    }

    public class ReceiveGameStartEventArgs : EventArgs
    {
        public TaikyokuShogi Game { get; }

        public ReceiveGameStartEventArgs(TaikyokuShogi game) => Game = game;
    }


    public class ShogiClient
    {
        private readonly HubConnection Connection;

        public delegate void ReceiveNewGameHandler(object sender, ReceiveNewGameEventArgs e);
        public event ReceiveNewGameHandler OnReceiveNewGame;

        public delegate void ReceiveGameListHandler(object sender, ReceiveGameListEventArgs e);
        public event ReceiveGameListHandler OnReceiveGameList;

        public delegate void ReceiveGameStartHandler(object sender, ReceiveGameStartEventArgs e);
        public event ReceiveGameStartHandler OnReceiveGameStart;

        public ShogiClient()
        {
            Connection = new HubConnectionBuilder().
                WithUrl("https://localhost:44352/ShogiHub").
                Build();

            Connection.Closed += async (error) =>
            {
                // manual reconnect on disco    nnect
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await Connection.StartAsync();
            };

            Connection.On<TaikyokuShogi, Guid>("ReceiveNewGame", (gameObject, gameId) =>
                OnReceiveNewGame?.Invoke(this, new ReceiveNewGameEventArgs(gameObject, gameId)));

            Connection.On<List<NetworkGameInfo>>("ReceiveGameList", gameList =>
                OnReceiveGameList?.Invoke(this, new ReceiveGameListEventArgs(gameList)));

            Connection.On<TaikyokuShogi>("ReceiveGameStart", gameObject =>
                OnReceiveGameStart?.Invoke(this, new ReceiveGameStartEventArgs(gameObject)));
        }

        public async Task ConnectAsync() =>
            await Connection.StartAsync();

        public async Task RequestGamesList() =>
            await Connection.InvokeAsync("GetGames");

        public async Task RequestNewGame(string gameName, TaikyokuShogiOptions gameOptions, bool asBlackPlayer) =>
            await Connection.InvokeAsync("CreateGame", gameName, gameOptions, asBlackPlayer);
    }
}
