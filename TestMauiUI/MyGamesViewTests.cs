using Xunit;

using MauiUI;
using ShogiEngine;

namespace TestMauiUI;

static class MockGuid
{
    public static Guid NewGuid(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}");
}

class MockLocalGamesManager : ILocalGamesManager
{
    public List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGames = new();

    public DateTime MockNow { get; set; } = DateTime.FromBinary(0);

    public MockLocalGamesManager() { }

    public event ILocalGamesManager.LocalGameUpdateHandler? OnLocalGameUpdate;

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

    public IEnumerable<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGameList { get => LocalGames; }
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

    [Fact]
    public void TestLocalGames()
    {
        // Test local games display
        //  TODO: Test (all) game state is displayed correctly
        //  TODO: Test (various) turn count is displayed correctly

        // List is purposely unsorted to ensure that the view sorts it.
        List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> localGames = new()
            {
                (MockGuid.NewGuid(1), DateTime.FromBinary(10000), new TaikyokuShogi()),
                (MockGuid.NewGuid(2), DateTime.FromBinary(0), new TaikyokuShogi()),
                (MockGuid.NewGuid(0), DateTime.FromBinary(20000), new TaikyokuShogi())
            };

        var localGamesManager = new MockLocalGamesManager()
        {
            LocalGames = localGames
        };

        var x = new MyGamesView(localGamesManager);

        var gameListView = x.FindByName<ListView>("GameListView");
        Assert.NotNull(gameListView);
        var cells = ((IVisualTreeElement)gameListView).GetVisualChildren();

        Assert.Equal(localGames.Count, cells.Count);
        Assert.Equal(localGames.Count, x.GamesList.Count);
        for (int i = 0; i < localGames.Count; ++i)
        {
            var dt = DateTime.FromBinary((2 - i) * 10000);

            var gameInfo = x.GamesList[i];
            Assert.True(gameInfo.Game?.BoardStateEquals(new TaikyokuShogi()) ?? false);
            Assert.Equal(MockGuid.NewGuid(i), gameInfo.GameId);
            Assert.True(gameInfo.IsLocal);
            Assert.Equal(dt, gameInfo.LastMoveOn);
            Assert.Equal(dt.ToString(), gameInfo.LastMove);
            Assert.Equal(PlayerColor.Black, gameInfo.MyColor);
            Assert.Equal("(local)", gameInfo.OpponentName);
            Assert.Equal(Guid.Empty, gameInfo.PlayerId);
            Assert.Equal("Black's Turn", gameInfo.Status);
            Assert.Equal("0", gameInfo.TurnCount);

            var cell = (ViewCell)cells[i];
            Assert.Equal("(local)", cell.View.FindByName<Label>("NameLbl").Text);
            Assert.Equal("Black's Turn", cell.View.FindByName<Label>("StatusLbl").Text);
            Assert.Equal(dt.ToString(), cell.View.FindByName<Label>("LastMoveLbl").Text);
            Assert.Equal("0", cell.View.FindByName<Label>("TurnCountLbl").Text);
        }
    }

    [Fact]
    public void TestLocalGamesSave()
    {
        // List is purposely unsorted to ensure that the view sorts it.
        List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> localGames = new()
            {
                (MockGuid.NewGuid(1), DateTime.FromBinary(100), new TaikyokuShogi()),
                (MockGuid.NewGuid(3), DateTime.FromBinary(300), new TaikyokuShogi()),
                (MockGuid.NewGuid(5), DateTime.FromBinary(500), new TaikyokuShogi())
            };

        var localGamesManager = new MockLocalGamesManager()
        {
            LocalGames = localGames
        };

        var x = new MyGamesView(localGamesManager);
        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(5), MockGuid.NewGuid(3), MockGuid.NewGuid(1) }
            );
        localGamesManager.MockNow = DateTime.FromBinary(600);
        localGamesManager.SaveGame(MockGuid.NewGuid(6), new TaikyokuShogi());
        localGamesManager.MockNow = DateTime.FromBinary(400);
        localGamesManager.SaveGame(MockGuid.NewGuid(4), new TaikyokuShogi());
        localGamesManager.MockNow = DateTime.FromBinary(200);
        localGamesManager.SaveGame(MockGuid.NewGuid(2), new TaikyokuShogi());
        localGamesManager.MockNow = DateTime.FromBinary(0);
        localGamesManager.SaveGame(MockGuid.NewGuid(0), new TaikyokuShogi());
        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(6), MockGuid.NewGuid(5), MockGuid.NewGuid(4), MockGuid.NewGuid(3), MockGuid.NewGuid(2), MockGuid.NewGuid(1), MockGuid.NewGuid(0) }
            );
    }

    [Fact]
    public void TestLocalGamesUpdate()
    {
        // List is purposely unsorted to ensure that the view sorts it.
        List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> localGames = new()
            {
                (MockGuid.NewGuid(1), DateTime.FromBinary(100), new TaikyokuShogi()),
                (MockGuid.NewGuid(3), DateTime.FromBinary(300), new TaikyokuShogi()),
                (MockGuid.NewGuid(5), DateTime.FromBinary(500), new TaikyokuShogi())
            };

        var localGamesManager = new MockLocalGamesManager()
        {
            LocalGames = localGames
        };

        var x = new MyGamesView(localGamesManager);
        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(5), MockGuid.NewGuid(3), MockGuid.NewGuid(1) }
            );

        localGamesManager.MockNow = DateTime.FromBinary(600);
        localGamesManager.SaveGame(MockGuid.NewGuid(5), new TaikyokuShogi());
        localGamesManager.MockNow = DateTime.FromBinary(601);
        localGamesManager.SaveGame(MockGuid.NewGuid(3), new TaikyokuShogi());
        localGamesManager.MockNow = DateTime.FromBinary(602);
        localGamesManager.SaveGame(MockGuid.NewGuid(1), new TaikyokuShogi());

        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(1), MockGuid.NewGuid(3), MockGuid.NewGuid(5) }
            );
    }

    [Fact]
    public void TestLocalGamesDelete()
    {
        // List is purposely unsorted to ensure that the view sorts it.
        List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> localGames = new()
            {
                (MockGuid.NewGuid(1), DateTime.FromBinary(100), new TaikyokuShogi()),
                (MockGuid.NewGuid(3), DateTime.FromBinary(300), new TaikyokuShogi()),
                (MockGuid.NewGuid(5), DateTime.FromBinary(500), new TaikyokuShogi())
            };

        var localGamesManager = new MockLocalGamesManager()
        {
            LocalGames = localGames
        };

        var x = new MyGamesView(localGamesManager);
        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(5), MockGuid.NewGuid(3), MockGuid.NewGuid(1) }
            );

        localGamesManager.DeleteGame(MockGuid.NewGuid(1));
        localGamesManager.DeleteGame(MockGuid.NewGuid(5));

        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(3) }
            );
    }

    // Test network games displayed correctly

    // Test mixed local/network games displayed correctly (sorted, etc.)
}