using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
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
    //
    // Bindable Proprerties
    //

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(IConnection), typeof(MyGamesView), null, BindingMode.OneWay, propertyChanged: OnConnectionChanged);

    public IConnection? Connection
    {
        get => (IConnection?)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    public ObservableCollection<MyGamesListItem> GamesList { get; set; } = new();

    private readonly ILocalGameSaver LocalGameSaver;

    private readonly INetworkGameSaver NetworkGameSaver;

    public MyGamesView() : this(null, null) { }

    public MyGamesView(ILocalGameSaver? localGameSaver, INetworkGameSaver? networkGameSaver)
    {
        InitializeComponent();

        LocalGameSaver = localGameSaver ?? MauiUI.LocalGameSaver.Default;
        NetworkGameSaver = networkGameSaver ?? MauiUI.NetworkGameSaver.Default;

        AddLocalGamesToMyGamesList();
        LocalGameSaver.OnLocalGameUpdate += LocalGameManager_OnLocalGameUpdate;
    }

    ~MyGamesView()
    {
        LocalGameSaver.OnLocalGameUpdate -= LocalGameManager_OnLocalGameUpdate;
        Connection = null;
    }

    private static async void OnConnectionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var me = (MyGamesView)bindable;

        // TODO: add listener to catch updates to my games
        //if (oldValue is Connection oldConnection)
        //    oldConnection -= some_listener;
        //if (newValue is Connection newConnection)
        //    newConnection += some_listener;

        await me.AddNetworkGamesToMyGamesList();
    }

    private void LocalGameManager_OnLocalGameUpdate(object sender, LocalGameUpdateEventArgs e)
    {
        if (e.Update == LocalGameUpdate.Add)
        {
            InsertSorted(MyGamesListItem.FromLocalGame(e.GameId, e.LastMove, e.Game!));
            return;
        }

        for (int i = 0; i < GamesList.Count; i++)
        {
            if (GamesList[i].GameId == e.GameId)
            {
                switch(e.Update)
                {
                    case LocalGameUpdate.Remove:
                        GamesList.RemoveAt(i);
                        break;
                    case LocalGameUpdate.Update:
                        GamesList.RemoveAt(i);
                        InsertSorted(MyGamesListItem.FromLocalGame(e.GameId, e.LastMove, e.Game!));
                        break;
                    default:
                        throw new Exception("Unknown update type");
                }
                return;
            }
        }

        throw new Exception("updated game not found...?");
    }

    private void AddLocalGamesToMyGamesList()
    {
        var localGameList = LocalGameSaver.LocalGameList.OrderByDescending(g => g.lastPlayed);
        foreach (var (gameId, lastPlayed, game) in localGameList)
        {
            GamesList.Add(MyGamesListItem.FromLocalGame(gameId, lastPlayed, game));
        }
    }

    private async Task AddNetworkGamesToMyGamesList()
    {
        if (Connection is null)
            return;

        try
        {
            var networkGameList = NetworkGameSaver.NetworkGameList;
            var requestInfos = networkGameList.Select(i => new NetworkGameRequest(i.GameId, i.PlayerId));
            var clientGameInfos = await Connection.RequestGameInfo(requestInfos);
            foreach (var (gameId, userId, myColor) in networkGameList)
            {
                var gameInfo = clientGameInfos.FirstOrDefault(g => g?.GameId == gameId, null);
                if (gameInfo is null)
                    NetworkGameSaver.DeleteGame(gameId, userId);
                else
                    InsertSorted(MyGamesListItem.FromNetworkGame(gameInfo, userId, myColor));
            }
        }
        catch (Exception ex) when (ShogiClient.Connection.ExceptionFilter(ex))
        {
            // TOOD: retry server communications later???
            // TODO: let the user know that network connection is down
        }
    }

    void InsertSorted(MyGamesListItem item)
    {
        // insert sorted
        for (int i = 0; i < GamesList.Count; i++)
        {
            if (GamesList[i].LastMoveOn < item.LastMoveOn)
            {
                GamesList.Insert(i, item);
                return;
            }
        }

        // insert at end
        GamesList.Add(item);
    }

    private async void GameListView_ItemTapped(object sender, ItemTappedEventArgs e)
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
            LocalGameSaver.DeleteGame(item.GameId);
        }
    }
}