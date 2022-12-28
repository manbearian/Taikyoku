using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using ShogiClient;
using ShogiComms;
using ShogiEngine;

namespace MauiUI;

public class GameListItem
{
    public string OpponentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastMove { get; set; } = string.Empty;
    public string TurnCount { get; set; } = string.Empty;

    public string Details { get; set; } = "...";

    public Guid GameId { get; set; } = Guid.Empty;

    public TaikyokuShogi Game { get; set; } = new TaikyokuShogi();

    public static GameListItem FromLocalGame(Guid gameId, DateTime lastPlayed, TaikyokuShogi game) =>
        new()
        {
            GameId = gameId,
            Game = game,
            OpponentName = "(local)",
            Status = game.CurrentPlayer switch
            {
                PlayerColor.Black => "Black's Turn",
                PlayerColor.White => "White's Turn",
                _ when game.Winner is not null => game.Winner switch
                {
                    PlayerColor.Black => "Black won",
                    PlayerColor.White => "White won",
                    _ => "drawn"
                },
                _ => "unknown"
            },
            LastMove = lastPlayed.ToString(),
            TurnCount = game.MoveCount.ToString()
        };

    public static GameListItem FromNetworkGame(ClientGameInfo gameInfo, PlayerColor myColor) =>
        new()
        {
            GameId = gameInfo.GameId,
            Game = new TaikyokuShogi(), // TODO: fix this
            OpponentName = (myColor == PlayerColor.Black ? gameInfo.WhiteName : gameInfo.BlackName) ?? "(unknown)",
            Status = gameInfo.Status switch
            {
                GameStatus.BlackTurn => "Black's Turn",
                GameStatus.WhiteTurn => "White's Turn",
                GameStatus.Expired => "expired",
                _ => "unknown"
            },
            LastMove = gameInfo.LastPlayed.ToString(),
            TurnCount = "-1" // TODO: Get actual game information
        };
}

public partial class MyGamesView : ContentView
{
    //
    // Bindable Proprerties
    //
    
    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(MyGamesView));

    public Connection Connection
    {
        get => (Connection)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    public ObservableCollection<GameListItem> GamesList { get; set; } = new();

    public MyGamesView()
    {
        InitializeComponent();

        Loaded += MyGamesView_Loaded;
        Unloaded += MyGamesView_Unloaded;
    }

    private void PopulateMyGamesList()
    {
        // TODO REMOVE THIS HACK
        MySettings.SaveGame(new TaikyokuShogi());
        MySettings.SaveGame(new TaikyokuShogi());
        MySettings.SaveGame(new TaikyokuShogi());

        var localGameList = MySettings.LocalGameList;
        foreach (var (gameId, lastPlayed, game) in localGameList)
        {
            GamesList.Add(GameListItem.FromLocalGame(gameId, lastPlayed, game));
        }

        // TODO REMOVE THIS HACK
        MySettings.ClearLocalGames();

        var networkGameList = MySettings.NetworkGameList;

        /////////////////////////////////////////////
        // TODO: REMOVE THIS HACK
        //
        Guid game1 = Guid.NewGuid();
        Guid game1player1 = Guid.NewGuid();
        Guid game1player2 = Guid.NewGuid();
        Guid game2 = Guid.NewGuid();
        Guid game2player1 = Guid.NewGuid();
        Guid game2player2 = Guid.NewGuid();
        Guid game3 = Guid.NewGuid();
        Guid game3player1 = Guid.NewGuid();
        Guid game3player2 = Guid.NewGuid();

        networkGameList = new (Guid GameId, Guid PlayerId, PlayerColor MyColor)[]
        {
            (game1, game1player1, PlayerColor.Black),
            (game1, game1player2, PlayerColor.White),
            (game2, game2player1, PlayerColor.White),
            (game3, game3player1, PlayerColor.Black),
        };
        //
        ///////////////////////////////////////////////////

        var requestInfos = networkGameList.Select(i => new NetworkGameRequest(i.GameId, i.PlayerId));
        // var clientGameInfos = await Connection.RequestGameInfo(requestInfos);
        ///////////////////////////////////
        // TODO REMOVE THIS THACK
        var clientGameInfos = new ClientGameInfo[]
        {
            new ClientGameInfo()
            {
                GameId = game1, BlackName = "alice", WhiteName = "bob", Created = DateTime.UtcNow, LastPlayed = DateTime.UtcNow, Status = GameStatus.BlackTurn
            },
            new ClientGameInfo()
            {
                GameId = game2,
                BlackName = "charlie",
                WhiteName = "dana",
                Created = DateTime.UtcNow,
                LastPlayed = DateTime.UtcNow,
                Status = GameStatus.BlackTurn
            }
        };
        //
        ///////////////////////////////
        foreach (var gameInfo in clientGameInfos)
        {
            var matchedGames = networkGameList.Where(i => i.GameId == gameInfo.GameId);
            Contract.Assert(matchedGames.Count() == 1 || matchedGames.Count() == 2);
            foreach (var (_, _, myColor) in matchedGames)
            {
                GamesList.Add(GameListItem.FromNetworkGame(gameInfo, myColor));
            }
        }

    }

    private void MyGamesView_Loaded(object? sender, EventArgs e) => 
        PopulateMyGamesList();

    private void MyGamesView_Unloaded(object? sender, EventArgs e)
    {
    }

}