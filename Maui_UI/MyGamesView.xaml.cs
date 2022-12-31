using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;

using ShogiComms;
using ShogiEngine;

namespace MauiUI;

public class MyGamesListItem
{
    public string OpponentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastMove { get; set; } = string.Empty;
    public string TurnCount { get; set; } = string.Empty;

    public Guid GameId { get; set; } = Guid.Empty;

    public TaikyokuShogi Game { get; set; } = new TaikyokuShogi();

    public bool IsLocal { get; set; } = true;

    public static MyGamesListItem FromLocalGame(Guid gameId, DateTime lastPlayed, TaikyokuShogi game) =>
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
            TurnCount = game.MoveCount.ToString(),
            IsLocal = true
        };

    public static MyGamesListItem FromNetworkGame(ClientGameInfo gameInfo, PlayerColor myColor) =>
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
            TurnCount = "-1", // TODO: Get actual game information
            IsLocal = false
        };
}

public partial class MyGamesView : ContentView
{
    public ObservableCollection<MyGamesListItem> GamesList { get; set; } = new();

    public MyGamesView()
    {
        InitializeComponent();

        Loaded += MyGamesView_Loaded;
        Unloaded += MyGamesView_Unloaded;
    }
    private async void MyGamesView_Loaded(object? sender, EventArgs e)
    {
        await PopulateMyGamesList();
        MySettings.LocalGameManager.OnLocalGameUpdate += LocalGameManager_OnLocalGameUpdate;
    }

    private void MyGamesView_Unloaded(object? sender, EventArgs e)
    {
        MySettings.LocalGameManager.OnLocalGameUpdate -= LocalGameManager_OnLocalGameUpdate;
    }

    private void LocalGameManager_OnLocalGameUpdate(object sender, LocalGameUpdateEventArgs e)
    {
        if (e.Update == LocalGameUpdate.Add)
            GamesList.Insert(0, MyGamesListItem.FromLocalGame(e.GameId, e.LastMove, e.Game!));

        for (int i = 0; i < GamesList.Count; i++)
        {
            if (GamesList[i].GameId == e.GameId)
            {
                if (e.Update == LocalGameUpdate.Remove)
                    GamesList.RemoveAt(i);
                else if (e.Update == LocalGameUpdate.Update)
                    GamesList[i] = MyGamesListItem.FromLocalGame(e.GameId, e.LastMove, e.Game!);
                return;
            }
        }

        throw new Exception("updated game not found...?");
    }

    private async Task PopulateMyGamesList()
    {
        var localGameList = LocalGameManager.LocalGameList;
        foreach (var (gameId, lastPlayed, game) in localGameList)
        {
            GamesList.Add(MyGamesListItem.FromLocalGame(gameId, lastPlayed, game));
        }

        var networkGameList = NetworkGameManager.NetworkGameList;

        var requestInfos = networkGameList.Select(i => new NetworkGameRequest(i.GameId, i.PlayerId));
        var clientGameInfos = await MainPage.Default.Connection.RequestGameInfo(requestInfos);
        foreach (var gameInfo in clientGameInfos)
        {
            var matchedGames = networkGameList.Where(i => i.GameId == gameInfo.GameId);
            Contract.Assert(matchedGames.Count() == 1 || matchedGames.Count() == 2);
            foreach (var (_, _, myColor) in matchedGames)
            {
                GamesList.Add(MyGamesListItem.FromNetworkGame(gameInfo, myColor));
            }
        }

        // TODO: Make the GamesList sorted
    }

    private void ListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
    {
        var item = (MyGamesListItem)e.SelectedItem;
        if (item is null)
            return;
        ((ListView)sender).SelectedItem = null;
        Navigation.PushModalAsync(new BoardPage(item.GameId, item.Game));
    }

    private async void DeleteBtn_Clicked(object sender, EventArgs e)
    {
        var item = (MyGamesListItem)((ImageButton)sender).BindingContext;
        Contract.Assert(item.IsLocal);

        // get parent page
        var parent = Parent;
        while (parent is not ContentPage)
        {
            parent = parent.Parent;
        }
        var parentPage = parent as ContentPage;
        Contract.Assert(parentPage is not null);

        bool confirmed = await parentPage.DisplayAlert("Delete Game?", "Would you like to delete this game?", "Yes", "No");
        if (confirmed)
        {
            MySettings.LocalGameManager.DeleteGame(item.GameId);
        }
    }
}