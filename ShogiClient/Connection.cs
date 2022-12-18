﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
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

        public ClientGameInfo GameInfo { get; }

        public Guid PlayerId { get; }

        public ReceiveGameStartEventArgs(TaikyokuShogi game, ClientGameInfo gameInfo, Guid playerId) =>
            (Game, GameInfo, PlayerId) = (game, gameInfo, playerId);
    }

    public class ReceiveGameConnectionEventArgs : EventArgs
    {
        public Guid GameId { get; }

        public ReceiveGameConnectionEventArgs(Guid gameId) => GameId = gameId;
    }

    public class ReceiveGameListEventArgs : EventArgs
    {
        public IEnumerable<ClientGameInfo> GameList { get; }

        public ReceiveGameListEventArgs(IEnumerable<ClientGameInfo> list) => GameList = list;
    }

    public class ReceiveEchoEventArgs : EventArgs
    {
        public string Message { get; }
        public ReceiveEchoEventArgs(string message) => Message = message;
    }

    public sealed class Connection : IDisposable
    {
        private readonly HubConnection _connection;

        public delegate void ReceiveGameListHandler(object sender, ReceiveGameListEventArgs e);
        public event ReceiveGameListHandler? OnReceiveGameList;

        public delegate void ReceiveGameStartHandler(object sender, ReceiveGameStartEventArgs e);
        public event ReceiveGameStartHandler? OnReceiveGameStart;

        public delegate void ReceiveGameUpdateHandler(object sender, ReceiveGameUpdateEventArgs e);
        public event ReceiveGameUpdateHandler? OnReceiveGameUpdate;

        public delegate void ReceiveGameDisconnectHandler(object sender, ReceiveGameConnectionEventArgs e);
        public event ReceiveGameDisconnectHandler? OnReceiveGameDisconnect;

        public delegate void ReceiveGameReconnectHandler(object sender, ReceiveGameConnectionEventArgs e);
        public event ReceiveGameReconnectHandler? OnReceiveGameReconnect;

        public delegate void ReceiveEchoHandler(object sender, ReceiveEchoEventArgs e);
        public event ReceiveEchoHandler? OnReceiveEcho;

        public Connection()
        {
            _connection = new HubConnectionBuilder().
                WithUrl("http://localhost:7071/api").
                WithAutomaticReconnect().
                Build();

            _connection.On<List<ClientGameInfo>>("ReceiveGameList", gameList =>
                OnReceiveGameList?.Invoke(this, new ReceiveGameListEventArgs(gameList)));

            _connection.On<string, ClientGameInfo, Guid>("ReceiveGameStart", (serializedGame, gameInfo, playerId) =>
                OnReceiveGameStart?.Invoke(this, new ReceiveGameStartEventArgs(serializedGame.ToTaikyokuShogi(), gameInfo, playerId)));

            _connection.On<string, Guid>("ReceiveGameUpdate", (serializedGame, gameId) =>
                OnReceiveGameUpdate?.Invoke(this, new ReceiveGameUpdateEventArgs(serializedGame.ToTaikyokuShogi(), gameId)));

            _connection.On<Guid>("ReceiveGameDisconnect", (gameId) =>
                OnReceiveGameDisconnect?.Invoke(this, new ReceiveGameConnectionEventArgs(gameId)));

            _connection.On<Guid>("ReceiveGameReconnect", (gameId) =>
                OnReceiveGameReconnect?.Invoke(this, new ReceiveGameConnectionEventArgs(gameId)));

            _connection.On<string>("Echo", (message) =>
                OnReceiveEcho?.Invoke(this, new ReceiveEchoEventArgs(message)));
        }

        public void Dispose() =>
            _connection.DisposeAsync().Wait();

        public async Task ConnectAsync()
        {
            try
            {
                await _connection.StartAsync();
            }
            catch (HttpRequestException)
            {
                // When Debugging it's posisble that the server hasn't been stood up yet
                // Sit here for 6 seconds (enough time to fully launch the Azure Functions) and try again
                await Task.Delay(6000).ContinueWith(t =>  _connection.StartAsync());
            }
        }

        public async Task<IEnumerable<ClientGameInfo>> RequestAllOpenGameInfo() =>
            await _connection.InvokeAsync<IEnumerable<ClientGameInfo>>("RequestAllOpenGameInfo");

        public async Task<IEnumerable<ClientGameInfo>> RequestGameInfo(IEnumerable<NetworkGameRequest> requests) =>
            await _connection.InvokeAsync<IEnumerable<ClientGameInfo>>("RequestGameInfo", requests.ToNetworkGameRequestList());

        public async Task<GamePlayerPair> RequestNewGame(string playerName, bool asBlackPlayer, TaikyokuShogi existingGame) =>
            await _connection.InvokeAsync<GamePlayerPair>("CreateGame", playerName, asBlackPlayer, existingGame.ToJsonString());

        public async Task JoinGame(Guid gameId, string playerName) =>
            await _connection.InvokeAsync<GamePlayerPair>("JoinGame", gameId, playerName);

        public async Task RequestRejoinGame(Guid gameId, Guid playerId) =>
            await _connection.InvokeAsync("RejoinGame", gameId, playerId);

        public async Task RequestCancelGame() =>
            await _connection.InvokeAsync("CancelGame");

        public async Task RequestMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc, bool promote) =>
            await _connection.InvokeAsync("RequestMove", (Location)startLoc, (Location)endLoc, (Location?)midLoc, promote);

        public async Task RequestResign() =>
            await _connection.InvokeAsync("RequestResign");

#if DEBUG
        public async Task Echo(string message) => await _connection.InvokeAsync("Echo", message);

        public async Task TestGameStart(TaikyokuShogi game) => await _connection.InvokeAsync("TestGameStart", game.ToJsonString());
#endif

        // Helper for identifying possible connection Exceptions
        public static bool ExceptionFilter(Exception e) =>
           e switch
           {
               _ when e is HubException => true,               // Marshalled exception from the hub.
               _ when e is InvalidOperationException => true,  // Connection failure
               _ when e is SocketException => true,            // Connection faliure
               _ when e is HttpRequestException => true,       // Connection faliure
               _ => false
           };
    }

    public static class ClientGameInfoExtension
    {
        public static PlayerColor UnassignedColor(this ClientGameInfo gameInfo)
        {
            if (gameInfo.BlackName == null && gameInfo.WhiteName == null)
                throw new Exception("bad state--no players");
            if (gameInfo.BlackName == null)
                return PlayerColor.Black;
            if (gameInfo.WhiteName == null)
                return PlayerColor.White;
            throw new Exception("bad state--full game");
        }

        public static string WaitingPlayerName(this ClientGameInfo gameInfo) => 
            gameInfo.PlayerName(gameInfo.UnassignedColor().Opponent());

        public static string PlayerName(this ClientGameInfo gameInfo, PlayerColor player) => player switch
        {
            PlayerColor.Black => gameInfo.BlackName ?? throw new Exception("no black player"),
            PlayerColor.White => gameInfo.WhiteName ?? throw new Exception("no whhite player"),
            _ => throw new NotSupportedException()
        };
    }
}