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

    internal Connection Connection { get; } = new();

    internal GameManager GameManager { get; } = new();

    private IDispatcherTimer NetworkReconnectTimer { get; }

    public MainPage()
    {
        InitializeComponent();

        PlayerNameEntry.Text = SettingsManager.Default.PlayerName == string.Empty ?
            Environment.UserName : SettingsManager.Default.PlayerName;

        Loaded += async (s, e) =>
        {
            Connection.OnReceiveGameStart += Connection_OnReceiveGameStart;
            GameManager.IsListening = true;
            await ConnectNetwork();
        };

        Unloaded += (s, e) =>
        {
            Connection.OnReceiveGameStart -= Connection_OnReceiveGameStart;
            GameManager.IsListening = false;
        };

        NetworkReconnectTimer = Dispatcher.CreateTimer();
        NetworkReconnectTimer.Interval = TimeSpan.FromSeconds(0.5);
        NetworkReconnectTimer.IsRepeating = false;
        NetworkReconnectTimer.Tick += async (s, e) => await ConnectNetwork();
    }

    private async Task ConnectNetwork()
    {
        try
        {
            await Connection.ConnectAsync();
            OnNetworkConnected?.Invoke(this, new EventArgs());
        }
        catch (Exception ex) when (Connection.ExceptionFilter(ex))
        {
            // failed to connect... that's okay try again in a bit
            var maxWait = TimeSpan.FromSeconds(32);
            NetworkReconnectTimer.Interval *= 2;
            if (NetworkReconnectTimer.Interval > maxWait)
                NetworkReconnectTimer.Interval = maxWait;
            NetworkReconnectTimer.Start();
        }
    }

    private async void NewLocalGameBtn_Clicked(object sender, EventArgs e) =>
        await LaunchGame(new());

    private void NewOnlineGameBtn_Clicked(object sender, EventArgs e) =>
        MainPageMode = MainPageMode.NewNetworkGame;

    private void JoinOnlineGameBtn_Clicked(object sender, EventArgs e) =>
        MainPageMode = MainPageMode.FindNetworkGame;

    private void Connection_OnReceiveGameStart(object sender, ReceiveGameStartEventArgs e)
    {
        NetworkGamesManager.Default.SaveGame(Connection.GameId, Connection.PlayerId, Connection.Color);
        var opponentName = (Connection.Color == PlayerColor.Black ? e.GameInfo.WhiteName : e.GameInfo.BlackName) ?? throw new Exception("Opponent name is null");
        GameManager.SetNetworkGame(e.Game, opponentName);
        Dispatcher.Dispatch(() =>
        {
            Navigation.PushModalAsync(BoardPage.Default, true);
            MainPageMode = MainPageMode.Home;
        });
    }

    // TODO: validate user name (not empty, not too long, etc.)
    private void PlayerNameEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlayerName = e.NewTextValue;
        SettingsManager.Default.PlayerName = e.NewTextValue;
    }

    public async Task LaunchGame(TaikyokuShogi game, Guid? gameId = null)
    {
        GameManager.SetLocalGame(game, gameId);
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