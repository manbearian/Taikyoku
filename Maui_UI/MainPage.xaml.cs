using System.Diagnostics.Contracts;
using System.Globalization;

using ShogiClient;
using ShogiEngine;

namespace MauiUI;

public enum MainPageMode
{
    Home, NewNetworkGame, FindNetworkGame, Wait
}

public partial class MainPage : ContentPage
{
    public delegate void NetworkConnectedHandler(object sender, EventArgs e);
    public event NetworkConnectedHandler? OnNetworkConnected;

    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty MainPageModeProperty = BindableProperty.Create(nameof(MainPageMode), typeof(MainPageMode), typeof(MyGamesView), MainPageMode.Home, BindingMode.OneWay);

    public MainPageMode MainPageMode
    {
        get => (MainPageMode)GetValue(MainPageModeProperty);
        set => SetValue(MainPageModeProperty, value);
    }

    public static readonly BindableProperty PlayerNameProperty = BindableProperty.Create(nameof(PlayerName), typeof(string), typeof(MyGamesView), string.Empty, BindingMode.OneWay);

    public string PlayerName
    {
        get => (string)GetValue(PlayerNameProperty);
        set => SetValue(PlayerNameProperty, value);
    }

    // The one and only MainPage
    public static MainPage Default { get; } = new();

    public Connection Connection { get; } = new();

    public TaikyokuShogi Game { get; private set; } = new TaikyokuShogi();

    public Guid? LocalGameId { get; private set; }

    public string OpponentName { get; private set; } = string.Empty;

    public bool IsLocalGame { get; private set; }

    public MainPage()
    {
        InitializeComponent();

        PlayerNameEntry.Text = SettingsManager.Default.PlayerName == string.Empty ?
            Environment.UserName : SettingsManager.Default.PlayerName;

        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    private async void MainPage_Loaded(object? sender, EventArgs e)
    {
        Connection.OnReceiveGameStart += Connection_OnReceiveGameStart;
        Connection.OnReceiveGameUpdate += Connection_OnReceiveGameUpdate;

        try
        {
            await Connection.ConnectAsync();
            OnNetworkConnected?.Invoke(this, new EventArgs());
        }
        catch (Exception ex) when (Connection.ExceptionFilter(ex))
        {
            // failed to connect... that's okay
            // TOOD: let the user know, allow them to try later
        }
    }

    private void MainPage_Unloaded(object? sender, EventArgs e)
    {
        Connection.OnReceiveGameStart -= Connection_OnReceiveGameStart;
        Connection.OnReceiveGameUpdate -= Connection_OnReceiveGameUpdate;
    }

    private async void NewLocalGameBtn_Clicked(object sender, EventArgs e) =>
        await LaunchGame(new());

    private void NewOnlineGameBtn_Clicked(object sender, EventArgs e) =>
        MainPageMode = MainPageMode.NewNetworkGame;

    private void JoinOnlineGameBtn_Clicked(object sender, EventArgs e) =>
        MainPageMode = MainPageMode.FindNetworkGame;

    private void Connection_OnReceiveGameStart(object sender, ReceiveGameStartEventArgs e)
    {
        SettingsManager.NetworkGameManager.SaveGame(Connection.GameId, Connection.PlayerId, Connection.Color);
        OpponentName = (Connection.Color == PlayerColor.Black ? e.GameInfo.WhiteName : e.GameInfo.BlackName) ?? throw new Exception("Opponent name is null");
        (Game, LocalGameId, IsLocalGame) = (e.Game, null, false);
        Dispatcher.Dispatch(() =>
        {
            Navigation.PushModalAsync(BoardPage.Default, true);
            MainPageMode = MainPageMode.Home;
        });
    }

    private void Connection_OnReceiveGameUpdate(object sender, ReceiveGameUpdateEventArgs e)
    {
        Contract.Assert(!IsLocalGame);
        Game = e.Game;
    }

    // TODO: validate user name (not empty, not too long, etc.)
    private void PlayerNameEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlayerName = e.NewTextValue;
        SettingsManager.Default.PlayerName = e.NewTextValue;
    }

    public async Task LaunchGame(TaikyokuShogi game, Guid? gameId = null)
    {
        (Game, LocalGameId, IsLocalGame) = (game, gameId, true);
        await Navigation.PushModalAsync(BoardPage.Default, true);
    }

    public async Task LaunchGame(Guid gameId, Guid playerId, PlayerColor myColor)
    {
        Connection.SetGameInfo(gameId, playerId, myColor);
        MainPageMode = MainPageMode.Wait;
        await Connection.RejoinGame();
    }
}

public class MainPageModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Enum.TryParse<MainPageMode>(parameter as string, out var mode) && mode == (MainPageMode)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One way conversion");
}