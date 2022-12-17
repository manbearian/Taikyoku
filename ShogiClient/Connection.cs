using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

using ShogiEngine;
using ShogiComms;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

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

        public Guid PlayerId { get; }

        public ReceiveGameStartEventArgs(TaikyokuShogi game, Guid gameId, Guid playerId) =>
            (Game, GameId, PlayerId) = (game, gameId, playerId);
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

            _connection.On<string, Guid, Guid>("ReceiveGameStart", (serializedGame, gameId, playerId) =>
                OnReceiveGameStart?.Invoke(this, new ReceiveGameStartEventArgs(serializedGame.ToTaikyokuShogi(), gameId, playerId)));

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

        public async Task ConnectAsync() =>
            await _connection.StartAsync();

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
    }

    public static class ClientGameInfoExtension
    {
        public static PlayerColor PlayerColor(this ClientGameInfo gameInfo) => Enum.TryParse<PlayerColor>(gameInfo.ClientColor, out var player) ? player : throw new NotSupportedException();

        public static string PlayerName(this ClientGameInfo gameInfo, PlayerColor? player = null) =>
            player switch
            {
                ShogiEngine.PlayerColor.Black => gameInfo.BlackName,
                ShogiEngine.PlayerColor.White => gameInfo.WhiteName,
                null => gameInfo.PlayerName(gameInfo.PlayerColor()),
                _ => throw new NotSupportedException()
            };

        public static PlayerColor OpponentColor(this ClientGameInfo gameInfo) => gameInfo.PlayerColor().Opponent();

        public static string OpponentName(this ClientGameInfo gameInfo) => gameInfo.PlayerName(gameInfo.OpponentColor());
    }
}