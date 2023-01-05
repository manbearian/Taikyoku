using System.Collections.ObjectModel;
using System.ComponentModel;

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
    // Bindabe Proprerties
    //

    public static readonly BindableProperty PlayerNameProperty = BindableProperty.Create(nameof(PlayerName), typeof(string), typeof(MyGamesView), string.Empty, BindingMode.OneWay);

    public string PlayerName
    {
        get => (string)GetValue(PlayerNameProperty);
        set => SetValue(PlayerNameProperty, value);
    }

    public ObservableCollection<FindGameListItem> GamesList { get; set; } = new();
    
    public FindGameView()
    {
        InitializeComponent();

        // Use visible/invisible as load/unload
        PropertyChanged += FindGameView_PropertyChanged;
    }

    private async void FindGameView_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IsVisible))
            return;
        if (sender is not FindGameView me || !me.IsLoaded)
            return;

        // Update display only when visible
        if (IsVisible)
        {
            MainPage.Default.Connection.OnReceiveGameList += Connection_OnReceiveGameList;
            await PopulateGamesList();
        }
        else
        {
            MainPage.Default.Connection.OnReceiveGameList -= Connection_OnReceiveGameList;
        }
    }

    private async void Connection_OnReceiveGameList(object sender, ReceiveGameListEventArgs e)
        => await Dispatcher.DispatchAsync(async () => await PopulateGamesList());

    private async Task PopulateGamesList()
    {
        GamesList.Clear();
        GamesList.Add(new FindGameListLoadingItem());

        try
        {
            var gameList = await MainPage.Default.Connection.RequestAllOpenGameInfo();
            var sortedList = gameList.OrderByDescending(g => g.LastPlayed);

            GamesList.Clear();
            foreach (var game in sortedList)
            {
                GamesList.Add(new FindGameListItem(game));
            }
        }
        catch(Exception ex) when (Connection.ExceptionFilter(ex))
        {
            GamesList.Clear();
            GamesList.Add(new FindGameListNullItem());
        }
    }

    private async void ListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
    {
        var item = (FindGameListItem)e.SelectedItem;
        if (item is null || item is FindGameListNullItem || item is FindGameListLoadingItem)
            return;
        ((ListView)sender).SelectedItem = null;
        MainPage.Default.Connection.SetGameInfo(item.GameInfo.GameId, Guid.Empty, item.GameInfo.UnassignedColor());
        // TODO: validate PlayerName
        await MainPage.Default.Connection.JoinGame(PlayerName);
        MainPage.Default.MainPageMode = MainPageMode.Wait;
    }

    private void CancelBtn_Clicked(object sender, EventArgs e) =>
        MainPage.Default.MainPageMode = MainPageMode.Home;
}