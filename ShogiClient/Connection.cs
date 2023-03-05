using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

using ShogiEngine;
using ShogiComms;

namespace ShogiClient;

public sealed class Connection : IConnection, IDisposable
{
    private readonly HubConnection _connection;

    public Guid GameId { get; private set; }

    public Guid PlayerId { get; private set; }

    public PlayerColor Color { get; private set; }

    public event IConnection.ReceiveGameListHandler? OnReceiveGameList;

    public event IConnection.ReceiveGameStartHandler? OnReceiveGameStart;

    public event IConnection.ReceiveGameUpdateHandler? OnReceiveGameUpdate;

    public event IConnection.ReceiveGameDisconnectHandler? OnReceiveGameDisconnect;

    public event IConnection.ReceiveGameReconnectHandler? OnReceiveGameReconnect;

    public event IConnection.ReceiveEchoHandler? OnReceiveEcho;

    public Connection() : this(Guid.Empty, Guid.Empty, PlayerColor.Black) { }

    public Connection(Guid gameId, Guid playerId, PlayerColor color)
    {
        (GameId, PlayerId, Color) = (gameId, playerId, color);

#if LOCAL
        _connection = new HubConnectionBuilder().
            WithUrl("http://localhost:7071/api").
            WithAutomaticReconnect().
            Build();
#else
        _connection = new HubConnectionBuilder().
            WithUrl("https://shogiserverless.azurewebsites.net/api").
            WithAutomaticReconnect().
            Build();
#endif

        _connection.On<List<ClientGameInfo>>("ReceiveGameList", gameList =>
            OnReceiveGameList?.Invoke(this, new ReceiveGameListEventArgs(gameList)));

        _connection.On<string, ClientGameInfo, Guid>("ReceiveGameStart", (serializedGame, gameInfo, playerId) =>
        {
            // invoke only on expected events
            if (gameInfo.GameId == GameId)
            {
                PlayerId = playerId;
                OnReceiveGameStart?.Invoke(this, new ReceiveGameStartEventArgs(serializedGame.ToTaikyokuShogi(), gameInfo, playerId));
            }
        });

        _connection.On<string, Guid>("ReceiveGameUpdate", (serializedGame, gameId) =>
        {
            if (gameId == GameId)
                OnReceiveGameUpdate?.Invoke(this, new ReceiveGameUpdateEventArgs(serializedGame.ToTaikyokuShogi(), gameId));
        });

        _connection.On<Guid>("ReceiveGameDisconnect", (gameId) =>
        {
            if (gameId == GameId)
                OnReceiveGameDisconnect?.Invoke(this, new ReceiveGameConnectionEventArgs(gameId));
        });

        _connection.On<Guid>("ReceiveGameReconnect", (gameId) =>
        {
            if (gameId == GameId)
                OnReceiveGameReconnect?.Invoke(this, new ReceiveGameConnectionEventArgs(gameId));
        });

        _connection.On<string>("Echo", (message) =>
            OnReceiveEcho?.Invoke(this, new ReceiveEchoEventArgs(message)));
    }

    public void Dispose() =>
        _connection.DisposeAsync().AsTask().Wait();

    public async Task ConnectAsync() => await ConnectAsync(0);

    private async Task ConnectAsync(int retry)
    {
        try
        {
            await _connection.StartAsync();
        }
        catch (HttpRequestException) when (retry < 3)
        {
            // When Debugging it's posisble that the server hasn't been stood up yet
            // It usually takes about 6 secons to start up. Try 3 times @ 3/6/9 second
            await Task.Delay(3000);
            await ConnectAsync(++retry);
        }
    }

    public async Task<IEnumerable<ClientGameInfo>> RequestAllOpenGameInfo() =>
        await _connection.InvokeAsync<IEnumerable<ClientGameInfo>>("RequestAllOpenGameInfo");

    public async Task<IEnumerable<ClientGameInfo>> RequestGameInfo(IEnumerable<NetworkGameRequest> requests) =>
        await _connection.InvokeAsync<IEnumerable<ClientGameInfo>>("RequestGameInfo", requests.ToNetworkGameRequestList());

    public async Task RequestNewGame(string playerName, bool asBlackPlayer, TaikyokuShogi existingGame)
    {
        Color = asBlackPlayer ? PlayerColor.Black : PlayerColor.White;
        (GameId, PlayerId) = await _connection.InvokeAsync<GamePlayerPair>("CreateGame", playerName, asBlackPlayer, existingGame.ToJsonString());
    }

    public async Task JoinGame(string playerName) =>
        await _connection.InvokeAsync<GamePlayerPair>("JoinGame", GameId, playerName);

    public async Task RejoinGame() =>
        await _connection.InvokeAsync("RejoinGame", GameId, PlayerId);

    public async Task RequestMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc, bool promote) =>
        await _connection.InvokeAsync("MakeMove", (Location)startLoc, (Location)endLoc, (Location)midLoc, promote);

    public async Task ResignGame() =>
        await _connection.InvokeAsync("ResignGame");
    
    public async Task CancelGame() =>
        await _connection.InvokeAsync("CancelGame");

#if DEBUG
    public async Task Echo(string message) => await _connection.InvokeAsync("Echo", message);

    public async Task TestGameStart(TaikyokuShogi game) => await _connection.InvokeAsync("TestGameStart", GameId, PlayerId, game.ToJsonString());
#endif

    // Helper for identifying possible connection Exceptions
    public static bool ExceptionFilter(Exception e) =>
       e switch
       {
           _ when e is HubException => true,               // Marshalled exception from the hub.
           _ when e is SocketException => true,            // Connection faliure
           _ when e is HttpRequestException => true,       // Connection faliure
           _ when e is InvalidOperationException => true,  // Connection failure
           _ => false
       };

    public void SetGameInfo(Guid gameId, Guid playerId, PlayerColor color) => (GameId, PlayerId, Color) = (gameId, playerId, color);
}

public static class ClientGameInfoExtension
{
    public static PlayerColor UnassignedColor(this ClientGameInfo gameInfo)
    {
        if (gameInfo.BlackName is null && gameInfo.WhiteName is null)
            throw new Exception("bad state--no players");
        if (gameInfo.BlackName is null)
            return PlayerColor.Black;
        if (gameInfo.WhiteName is null)
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
