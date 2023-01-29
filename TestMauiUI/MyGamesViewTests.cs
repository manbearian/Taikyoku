using Xunit;

using MauiUI;
using ShogiEngine;

namespace TestMauiUI;

class MockLocalGamesManager : ILocalGamesManager
{
    private List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> localGames;

    public DateTime MockNow { get; set; } = DateTime.FromBinary(0);

    public MockLocalGamesManager(List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)>? games = null)
    {
        localGames = games ?? new();
    }

    public event ILocalGamesManager.LocalGameUpdateHandler? OnLocalGameUpdate;

    public void SaveGame(Guid gameId, TaikyokuShogi game) { localGames.Add((gameId, MockNow, game)); }

    public void DeleteGame(Guid gameId) { localGames.RemoveAll(entry => entry.GameId == gameId); }

    public IEnumerable<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGameList { get => localGames; }
}

public class MyGamesViewTests : BaseTest
{
    [Fact]
    public void TestConstruction()
    {
        var localGamesManager = new MockLocalGamesManager();

        var x = new MyGamesView(localGamesManager);

        var gameListView = x.FindByName<ListView>("GameListView");
        Assert.NotNull(gameListView);
        Assert.Empty(x.GamesList);
        Assert.Empty(((IVisualTreeElement)gameListView).GetVisualChildren());

    }

    // Test local games displayed correctly

    // Test network games displayed correctly

}