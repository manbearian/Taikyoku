using Xunit;

using MauiUI;
using ShogiEngine;

namespace TestMauiUI;

class MockLocalGameSaver : ILocalGameSaver
{
    public List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGames = new();

    public DateTime MockNow { get; set; } = DateTime.FromBinary(0);

    public MockLocalGameSaver() { }

    public event ILocalGameSaver.LocalGameUpdateHandler? OnLocalGameUpdate;

    public void SaveGame(Guid gameId, TaikyokuShogi game)
    {
        if (LocalGames.Exists(elem => elem.GameId == gameId))
        {
            var removed = LocalGames.RemoveAll(elem => elem.GameId == gameId);
            Assert.Equal(1, removed);
            OnLocalGameUpdate?.Invoke(this, new LocalGameUpdateEventArgs(gameId, game, MockNow, LocalGameUpdate.Update));
        }
        else
        {
            LocalGames.Add((gameId, MockNow, game));
            OnLocalGameUpdate?.Invoke(this, new LocalGameUpdateEventArgs(gameId, game, MockNow, LocalGameUpdate.Add));
        }
    }

    public void DeleteGame(Guid gameId)
    {
        var removed = LocalGames.RemoveAll(entry => entry.GameId == gameId);
        Assert.Equal(1, removed);
        OnLocalGameUpdate?.Invoke(this, new LocalGameUpdateEventArgs(gameId, null, MockNow, LocalGameUpdate.Remove));
    }

    public IEnumerable<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGameList
    { 
        get => LocalGames.ToArray(); // return a copy to prevent invalidation during iteration
    }
}

class MockNewtorkGameSaver : INetworkGameSaver
{
    public List<(Guid GameId, Guid PlayerId, PlayerColor MyColor)> NetworkGames = new();

    public void SaveGame(Guid gameId, Guid playerId, PlayerColor myColor)
    {
        NetworkGames.Add((gameId, playerId, myColor));
    }

    public void DeleteGame(Guid gameId, Guid playerId)
    {
        var removed = NetworkGames.RemoveAll(entry => entry.GameId == gameId && entry.PlayerId == playerId);
        Assert.Equal(1, removed);
    }

    public IEnumerable<(Guid GameId, Guid PlayerId, PlayerColor MyColor)> NetworkGameList
    {
        get => NetworkGames.ToList().ToArray(); // return a copy to prevent invalidation during iteration
    }
}
