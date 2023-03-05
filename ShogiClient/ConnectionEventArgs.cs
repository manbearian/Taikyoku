using System;
using System.Collections.Generic;

using ShogiComms;
using ShogiEngine;

namespace ShogiClient;

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
