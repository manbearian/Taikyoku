using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;

using ShogiClient;
using ShogiComms;
using ShogiEngine;

namespace MauiUI;

public class MyGamesListItem
{
    public string OpponentName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string LastMove { get => LastMoveOn?.ToLocalTime().ToString() ?? string.Empty; }

    public string TurnCount { get => Game?.MoveCount.ToString() ?? string.Empty; }

    public Guid GameId { get; set; } = Guid.Empty;
    
    public Guid PlayerId { get; set; } = Guid.Empty;

    public PlayerColor MyColor { get; set; } = PlayerColor.Black;

    public TaikyokuShogi? Game { get; set; } = null;

    public DateTime? LastMoveOn { get; set; } = DateTime.MinValue;

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
            LastMoveOn = lastPlayed,
            IsLocal = true
        };

    public static MyGamesListItem FromNetworkGame(ClientGameInfo gameInfo, Guid playerId, PlayerColor myColor) =>
        new()
        {
            GameId = gameInfo.GameId,
            PlayerId = playerId,
            MyColor = myColor,
            OpponentName = (myColor == PlayerColor.Black ? gameInfo.WhiteName : gameInfo.BlackName) ?? string.Empty,
            Status = gameInfo.Status switch
            {
                GameStatus.BlackTurn when myColor == PlayerColor.Black => "Your Turn",
                GameStatus.BlackTurn => "Their Turn",
                GameStatus.WhiteTurn when myColor == PlayerColor.White => "Your Turn",
                GameStatus.WhiteTurn => "Their Turn",
                GameStatus.Completed => "Completed",
                GameStatus.Expired => "Expired",
                _ => "(unknown)"
            },
            LastMoveOn = gameInfo.LastPlayed,
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
        SettingsManager.LocalGameManager.OnLocalGameUpdate += LocalGameManager_OnLocalGameUpdate;
        MainPage.Default.OnNetworkConnected += Default_OnNetworkConnected;
    }

    private void MyGamesView_Unloaded(object? sender, EventArgs e)
    {
        SettingsManager.LocalGameManager.OnLocalGameUpdate -= LocalGameManager_OnLocalGameUpdate;
        MainPage.Default.OnNetworkConnected -= Default_OnNetworkConnected;
    }

    private async void Default_OnNetworkConnected(object sender, EventArgs e) =>
        await AddNetworkGamesToMyGamesList();

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

    private void AddLocalGamesToMyGamesList()
    {
        var localGameList = LocalGameManager.LocalGameList.OrderByDescending(g => g.lastPlayed);
        foreach (var (gameId, lastPlayed, game) in localGameList)
        {
            GamesList.Add(MyGamesListItem.FromLocalGame(gameId, lastPlayed, game));
        }
    }

    private async Task AddNetworkGamesToMyGamesList()
    {
        try
        {
            var networkGameList = NetworkGameManager.NetworkGameList;
            var requestInfos = networkGameList.Select(i => new NetworkGameRequest(i.GameId, i.PlayerId));
            var clientGameInfos = await MainPage.Default.Connection.RequestGameInfo(requestInfos);
            foreach (var (gameId, userId, myColor) in networkGameList)
            {
                var gameInfo = clientGameInfos.FirstOrDefault(g => g?.GameId == gameId, null);
                if (gameInfo is null)
                    SettingsManager.NetworkGameManager.DeleteGame(gameId, userId);
                else
                    InsertSorted(MyGamesListItem.FromNetworkGame(gameInfo, userId, myColor));
            }
        }
        catch (Exception ex) when (Connection.ExceptionFilter(ex))
        {
            // TOOD: retry server communications later???
            // TODO: let the user know that network connection is down
        }

        void InsertSorted(MyGamesListItem item)
        {
            GamesList.Add(item);

            // Inserting at the head of the list seems to have a bug that causes the ItemList
            // to duplicate the inesrted item and not display other items, so do this insert-at-end
            // and swap dance.
            for (int i = 0; i < GamesList.Count; i++)
            {
                if (GamesList[i].LastMoveOn < item.LastMoveOn)
                {
                    GamesList.Move(GamesList.Count - 1, i);
                    return;
                }
            }

        }
    }

    private async Task PopulateMyGamesList()
    {
        AddLocalGamesToMyGamesList();
        await AddNetworkGamesToMyGamesList();
    }

    private async void ListView_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        var item = (MyGamesListItem)e.Item;
        if (item is null)
            return;

        if (item.IsLocal)
        {
            Contract.Assert(item.Game is not null);
            await MainPage.Default.LaunchGame(item.Game, item.GameId);
        }
        else
        {
            await MainPage.Default.LaunchGame(item.GameId, item.PlayerId, item.MyColor);
        }
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
            SettingsManager.LocalGameManager.DeleteGame(item.GameId);
        }
    }
}