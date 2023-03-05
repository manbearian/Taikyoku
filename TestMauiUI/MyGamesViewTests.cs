using Xunit;

using MauiUI;
using ShogiEngine;
using ShogiComms;
using Xunit.Abstractions;
using Microsoft.VisualStudio.TestPlatform.Utilities;

namespace TestMauiUI;

public class MyGamesViewTests : BaseTest
{
    private readonly ITestOutputHelper output;

    public MyGamesViewTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void TestConstruction()
    {
        var localGameSaver = new MockLocalGameSaver();
        var networkGameSaver = new MockNewtorkGameSaver();

        var x = new MyGamesView(localGameSaver, networkGameSaver);

        var gameListView = x.FindByName<ListView>("GameListView");
        Assert.NotNull(gameListView);
        Assert.Empty(x.GamesList);
        Assert.Empty(((IVisualTreeElement)gameListView).GetVisualChildren());

    }

    [Fact]
    public void TestLocalGamesDisplay()
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

        var localGameSaver = new MockLocalGameSaver()
        {
            LocalGames = localGames
        };
        var networkGameSaver = new MockNewtorkGameSaver();

        var x = new MyGamesView(localGameSaver, networkGameSaver);

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

        var localGameSaver = new MockLocalGameSaver()
        {
            LocalGames = localGames
        };
        var networkGameSaver = new MockNewtorkGameSaver();

        var x = new MyGamesView(localGameSaver, networkGameSaver);
        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(5), MockGuid.NewGuid(3), MockGuid.NewGuid(1) }
            );
        localGameSaver.MockNow = DateTime.FromBinary(600);
        localGameSaver.SaveGame(MockGuid.NewGuid(6), new TaikyokuShogi());
        localGameSaver.MockNow = DateTime.FromBinary(400);
        localGameSaver.SaveGame(MockGuid.NewGuid(4), new TaikyokuShogi());
        localGameSaver.MockNow = DateTime.FromBinary(200);
        localGameSaver.SaveGame(MockGuid.NewGuid(2), new TaikyokuShogi());
        localGameSaver.MockNow = DateTime.FromBinary(0);
        localGameSaver.SaveGame(MockGuid.NewGuid(0), new TaikyokuShogi());
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

        var localGameSaver = new MockLocalGameSaver()
        {
            LocalGames = localGames
        };
        var networkGameSaver = new MockNewtorkGameSaver();

        var x = new MyGamesView(localGameSaver, networkGameSaver);
        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(5), MockGuid.NewGuid(3), MockGuid.NewGuid(1) }
            );

        localGameSaver.MockNow = DateTime.FromBinary(600);
        localGameSaver.SaveGame(MockGuid.NewGuid(5), new TaikyokuShogi());
        localGameSaver.MockNow = DateTime.FromBinary(601);
        localGameSaver.SaveGame(MockGuid.NewGuid(3), new TaikyokuShogi());
        localGameSaver.MockNow = DateTime.FromBinary(602);
        localGameSaver.SaveGame(MockGuid.NewGuid(1), new TaikyokuShogi());

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

        var localGameSaver = new MockLocalGameSaver()
        {
            LocalGames = localGames
        };
        var networkGameSaver = new MockNewtorkGameSaver();

        var x = new MyGamesView(localGameSaver, networkGameSaver);

        localGameSaver.DeleteGame(MockGuid.NewGuid(1));
        localGameSaver.DeleteGame(MockGuid.NewGuid(5));

        Assert.Equal(
            x.GamesList.Select(gameInfo => gameInfo.GameId),
            new List<Guid> { MockGuid.NewGuid(3) }
            );
    }

    [Fact]
    public void TestNetworkGamesDisplay()
    {
        // Test netwrok games display
        //  TODO: Test non-active game state is displayed correctly
        //  TODO: Test turn count is displayed correctly (when available from server)

        List<(Guid GameId, Guid PlayerId, PlayerColor MyColor)> networkGames = new()
            {
                (MockGuid.NewGuid(0), MockGuid.NewGuid(10), PlayerColor.Black),
                (MockGuid.NewGuid(1), MockGuid.NewGuid(11), PlayerColor.Black),
                (MockGuid.NewGuid(3), MockGuid.NewGuid(13), PlayerColor.White),
                (MockGuid.NewGuid(4), MockGuid.NewGuid(14), PlayerColor.White) // this game won't be found
            };

        Dictionary<Guid, ClientGameInfo> gameDatabase = new()
            {
                {
                    MockGuid.NewGuid(0),
                        new()
                        {
                            GameId = MockGuid.NewGuid(0),
                            BlackName = "Frank",
                            WhiteName = "Lucy",
                            Created =  DateTime.FromBinary(10000),
                            LastPlayed =  DateTime.FromBinary(30000),
                            Status = GameStatus.BlackTurn
                        }
                },
                {
                    MockGuid.NewGuid(1),
                        new()
                        {
                            GameId = MockGuid.NewGuid(1),
                            BlackName = "Ed",
                            WhiteName = "Torrent",
                            Created =  DateTime.FromBinary(1),
                            LastPlayed =  DateTime.FromBinary(10000),
                            Status = GameStatus.WhiteTurn
                        }
                },
                {
                    // this game shouldn't be found
                    MockGuid.NewGuid(2),
                        new()
                        {
                            GameId = MockGuid.NewGuid(2),
                            BlackName = "Boogie",
                            WhiteName = "Woogie",
                            Created =  DateTime.FromBinary(9999),
                            LastPlayed =  DateTime.FromBinary(999999),
                            Status = GameStatus.WhiteTurn
                        }
                },
                {
                    MockGuid.NewGuid(3),
                        new()
                        {
                            GameId = MockGuid.NewGuid(3),
                            BlackName = "Tanaka",
                            WhiteName = "Natasha",
                            Created =  DateTime.FromBinary(9999),
                            LastPlayed =  DateTime.FromBinary(20000),
                            Status = GameStatus.BlackTurn
                        }
                }
            };

        var localGameSaver = new MockLocalGameSaver();
        var networkGameSaver = new MockNewtorkGameSaver()
        {
            NetworkGames = networkGames
        };
        var connection = new MockConnection()
        {
            _connected = true,
            GameDatabase = gameDatabase
        };

        using AutoResetEvent uiUpdated = new(false);

        var x = new MyGamesView(localGameSaver, networkGameSaver)
        {
            Connection = connection
        };

        var gameListView = x.FindByName<ListView>("GameListView");
        Assert.NotNull(gameListView);

        // wait until the UI is updated
        var cells = ((IVisualTreeElement)gameListView).GetVisualChildren();
        while (true)
        {
            if (cells.Count == networkGames.Count)
                break;
            cells = ((IVisualTreeElement)gameListView).GetVisualChildren();
        }

        Assert.Equal(networkGames.Count, cells.Count);
        Assert.Equal(networkGames.Count, x.GamesList.Count);
        for (int i = 0; i < networkGames.Count; ++i)
        {
            // Check that the game is displayed in the correct order based on sorting via last-played
            var gameId = new int[] { 0, 3, 1 }[i];
            var dt = DateTime.FromBinary((3 - i) * 10000);
            var oppName = new string[] { "Lucy", "Torrent", "Boogie", "Tanaka" }[gameId];
            var turn = new string[] { "Your Turn", "Their Turn", "", "Their Turn" }[gameId];
            var myColor = new PlayerColor[] { PlayerColor.Black, PlayerColor.Black, PlayerColor.White, PlayerColor.White }[gameId];

            var gameInfo = x.GamesList[i];
            Assert.Null(gameInfo.Game);
            Assert.Equal(MockGuid.NewGuid(gameId), gameInfo.GameId);
            Assert.False(gameInfo.IsLocal);
            Assert.Equal(dt, gameInfo.LastMoveOn);
            Assert.Equal(dt.ToString(), gameInfo.LastMove);
            Assert.Equal(myColor, gameInfo.MyColor);
            Assert.Equal(oppName, gameInfo.OpponentName);
            Assert.Equal(MockGuid.NewGuid(10 + gameId), gameInfo.PlayerId);
            Assert.Equal(turn, gameInfo.Status);
            Assert.Equal("", gameInfo.TurnCount);

            // this is broken?!? it appears the children aren't sorted here;
            // are they displayed sorted? how do i test this properly???
            //var cell = (ViewCell)cells[i];
            //Assert.Equal(oppName, cell.View.FindByName<Label>("NameLbl").Text);
            //Assert.Equal(turn, cell.View.FindByName<Label>("StatusLbl").Text);
            //Assert.Equal(dt.ToString(), cell.View.FindByName<Label>("LastMoveLbl").Text);
            //Assert.Equal("", cell.View.FindByName<Label>("TurnCountLbl").Text);
        }
    }

    private void X_ChildAdded(object? sender, ElementEventArgs e)
    {
        throw new NotImplementedException();
    }


    // Test mixed local/network games displayed correctly (sorted, etc.)
}