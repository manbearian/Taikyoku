using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using ShogiClient;
using ShogiComms;
using ShogiEngine;

namespace MauiUI;

public class FindGameListItem
{
    public virtual string OpponentName { get => GameInfo.WaitingPlayerName(); }
    public virtual string MyColor { get => GameInfo.UnassignedColor().ToString(); }
    public virtual string CreatedOn { get => GameInfo.LastPlayed.ToLocalTime().ToString(); }

    public ClientGameInfo GameInfo { get; }

    public FindGameListItem(ClientGameInfo g) => GameInfo = g;
}

public class FindGameListNullItem : FindGameListItem
{
    public override string OpponentName { get => "Connection Unavailable"; }
    public override string MyColor { get => ""; }
    public override string CreatedOn { get => ""; }
    public FindGameListNullItem() : base(new ClientGameInfo()) { }
}

public class FindGameListLoadingItem : FindGameListItem
{
    public override string OpponentName { get => "Connecting..."; }
    public override string MyColor { get => ""; }
    public override string CreatedOn { get => ""; }
    public FindGameListLoadingItem() : base(new ClientGameInfo()) { }
}

public partial class FindGameView : ContentView
{
    //
    // Bindable Proprerties
    //

    public static readonly BindableProperty PlayerNameProperty = BindableProperty.Create(nameof(PlayerName), typeof(string), typeof(FindGameView), string.Empty, BindingMode.OneWay);

    public string PlayerName
    {
        get => (string)GetValue(PlayerNameProperty);
        set => SetValue(PlayerNameProperty, value);
    }

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(IConnection), typeof(MyGamesView), null, BindingMode.OneWay, propertyChanged: OnConnectionChanged);

    public IConnection? Connection
    {
        get => (IConnection?)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    public ObservableCollection<FindGameListItem> GamesList { get; set; } = new();
    
    public FindGameView()
    {
        InitializeComponent();
    }

    ~FindGameView()
    {
        Connection = null;
    }

    private static async void OnConnectionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var me = (FindGameView)bindable;

        if (oldValue is Connection oldConnection)
            oldConnection.OnReceiveGameList -= me.Connection_OnReceiveGameList;
        if (newValue is Connection newConnection)
            newConnection.OnReceiveGameList += me.Connection_OnReceiveGameList;
        await me.PopulateGamesList();
    }

    private async void Connection_OnReceiveGameList(object sender, ReceiveGameListEventArgs e)
        => await Dispatcher.DispatchAsync(async () => await PopulateGamesList(e.GameList));

    private async Task PopulateGamesList(IEnumerable<ClientGameInfo>? gameList = null)
    {
        GamesList.Clear();
        GamesList.Add(new FindGameListLoadingItem());

        try
        {
            if (Connection is null)
                return;
            gameList ??= await Connection.RequestAllOpenGameInfo();
            var sortedList = gameList.OrderByDescending(g => g.LastPlayed);

            GamesList.Clear();
            foreach (var game in sortedList)
            {
                GamesList.Add(new FindGameListItem(game));
            }
        }
        catch(Exception ex) when (ShogiClient.Connection.ExceptionFilter(ex))
        {
            GamesList.Clear();
            GamesList.Add(new FindGameListNullItem());
        }
    }

    private async void ListView_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        var item = (FindGameListItem)e.Item;
        if (item is null || item is FindGameListNullItem || item is FindGameListLoadingItem)
            return;
        if (Connection is null)
            return;
        Connection.SetGameInfo(item.GameInfo.GameId, Guid.Empty, item.GameInfo.UnassignedColor());
        // TODO: validate PlayerName
        await Connection.JoinGame(PlayerName);
        MainPage.Default.MainPageMode = MainPageMode.Wait;
    }

    private void CancelBtn_Clicked(object sender, EventArgs e) =>
        MainPage.Default.MainPageMode = MainPageMode.Home;
}