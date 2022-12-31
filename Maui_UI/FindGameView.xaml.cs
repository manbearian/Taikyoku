using System.Collections.ObjectModel;
using System.ComponentModel;

using ShogiClient;
using ShogiComms;
using ShogiEngine;

namespace MauiUI;

public class FindGameListItem
{
    public string OpponentName { get => GameInfo.WaitingPlayerName(); }
    public string MyColor { get => GameInfo.UnassignedColor().ToString(); }
    public string CreatedOn { get => GameInfo.LastPlayed.ToLocalTime().ToString(); }

    public ClientGameInfo GameInfo { get; }

    public FindGameListItem(ClientGameInfo g) => GameInfo = g;
}

public partial class FindGameView : ContentView
{
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
    {
        await Dispatcher.DispatchAsync(async () =>
        {
            GamesList.Clear();
            await PopulateGamesList();
        });
    }

    private async Task PopulateGamesList()
    {
        var gameList = await MainPage.Default.Connection.RequestAllOpenGameInfo();
        var sortedList = gameList.OrderByDescending(g => g.LastPlayed);
        foreach(var game in sortedList)
        {
            GamesList.Add(new FindGameListItem(game));
        }
    }

    private async void ListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
    {
        var item = (FindGameListItem)e.SelectedItem;
        if (item is null)
            return;
        ((ListView)sender).SelectedItem = null;
        MainPage.Default.Connection.SetGameInfo(item.GameInfo.GameId, Guid.Empty, item.GameInfo.UnassignedColor());
        await MainPage.Default.Connection.JoinGame("no name yet"); // TOOD: Get a name
        MainPage.Default.MainPageMode = MainPageMode.Wait;
    }
}