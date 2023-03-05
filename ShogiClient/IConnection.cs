using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;

using ShogiComms;
using ShogiEngine;

namespace ShogiClient;

public interface IConnection
{
    Guid GameId { get; }

    Guid PlayerId { get;  }

    PlayerColor Color { get; }

    delegate void ReceiveGameListHandler(object sender, ReceiveGameListEventArgs e);
    event ReceiveGameListHandler? OnReceiveGameList;

    delegate void ReceiveGameStartHandler(object sender, ReceiveGameStartEventArgs e);
    event ReceiveGameStartHandler? OnReceiveGameStart;

    delegate void ReceiveGameUpdateHandler(object sender, ReceiveGameUpdateEventArgs e);
    event ReceiveGameUpdateHandler? OnReceiveGameUpdate;

    delegate void ReceiveGameDisconnectHandler(object sender, ReceiveGameConnectionEventArgs e);
    event ReceiveGameDisconnectHandler? OnReceiveGameDisconnect;

    delegate void ReceiveGameReconnectHandler(object sender, ReceiveGameConnectionEventArgs e);
    event ReceiveGameReconnectHandler? OnReceiveGameReconnect;

    delegate void ReceiveEchoHandler(object sender, ReceiveEchoEventArgs e);
    event ReceiveEchoHandler? OnReceiveEcho;

    Task ConnectAsync();

    Task<IEnumerable<ClientGameInfo>> RequestAllOpenGameInfo();

    Task<IEnumerable<ClientGameInfo>> RequestGameInfo(IEnumerable<NetworkGameRequest> requests);

    Task RequestNewGame(string playerName, bool asBlackPlayer, TaikyokuShogi existingGame);

    Task JoinGame(string playerName);

    Task RejoinGame();

    Task RequestMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc, bool promote);

    Task ResignGame();

    Task CancelGame();

    void SetGameInfo(Guid gameId, Guid playerId, PlayerColor color);

    bool IsGameNotFoundException(HubException ex) =>
        ex.Message == string.Format(HubExceptions.OpenGameNotFound, GameId);
}
