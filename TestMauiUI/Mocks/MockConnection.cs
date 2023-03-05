using Xunit;

using ShogiClient;
using ShogiComms;
using ShogiEngine;

namespace TestMauiUI;

internal class MockConnection : IConnection
{
    public bool _connected = false;

    public Dictionary<Guid, ClientGameInfo> GameDatabase { get; set; } = new();

    public Guid GameId { get; private set; }

    public Guid PlayerId { get; private set; }

    public PlayerColor Color { get; private set; }

    public event IConnection.ReceiveGameListHandler? OnReceiveGameList;

    public event IConnection.ReceiveGameStartHandler? OnReceiveGameStart;

    public event IConnection.ReceiveGameUpdateHandler? OnReceiveGameUpdate;

    public event IConnection.ReceiveGameDisconnectHandler? OnReceiveGameDisconnect;

    public event IConnection.ReceiveGameReconnectHandler? OnReceiveGameReconnect;

    public event IConnection.ReceiveEchoHandler? OnReceiveEcho;

    public MockConnection()
    {

    }

    public Task ConnectAsync() =>
        Task.Run(() => { _connected = true; });

    public Task<IEnumerable<ClientGameInfo>> RequestAllOpenGameInfo() =>
        Task.Run(() =>
        {
            Assert.True(_connected);
            return GameDatabase.Values as IEnumerable<ClientGameInfo>;
        });

    public Task<IEnumerable<ClientGameInfo>> RequestGameInfo(IEnumerable<NetworkGameRequest> requests) =>
        Task.Run(() =>
        {
            Assert.True(_connected);
            return requests.ToArray().Select(request => GameDatabase.GetValueOrDefault(request.GameId)).Where(v => v is not null) as IEnumerable<ClientGameInfo>;
        });

    public Task RequestNewGame(string playerName, bool asBlackPlayer, TaikyokuShogi existingGame) =>
        throw new NotImplementedException();

    public Task JoinGame(string playerName) =>
        throw new NotImplementedException();

    public Task RejoinGame() =>
        throw new NotImplementedException();

    public Task RequestMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc, bool promote) =>
        throw new NotImplementedException();

    public Task ResignGame() =>
        throw new NotImplementedException();

    public Task CancelGame() =>
        throw new NotImplementedException();

    public void SetGameInfo(Guid gameId, Guid playerId, PlayerColor color) =>
        (GameId, PlayerId, Color) = (gameId, playerId, color);
}
