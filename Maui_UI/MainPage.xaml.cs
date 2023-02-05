using System.Diagnostics.Contracts;
using System.Globalization;
using System.Runtime.CompilerServices;
using ShogiClient;
using ShogiEngine;

namespace MauiUI;

public enum MainPageMode
{
    Home, NewNetworkGame, FindNetworkGame, Wait
}

public partial class MainPage : ContentPage
{
    //
    // Bindable Proprerties
    //

    public static readonly BindableProperty MainPageModeProperty = BindableProperty.Create(nameof(MainPageMode), typeof(MainPageMode), typeof(MainPage), MainPageMode.Home, BindingMode.OneWay);

    public MainPageMode MainPageMode
    {
        get => (MainPageMode)GetValue(MainPageModeProperty);
        set => SetValue(MainPageModeProperty, value);
    }

    public static readonly BindableProperty PlayerNameProperty = BindableProperty.Create(nameof(PlayerName), typeof(string), typeof(MainPage), string.Empty, BindingMode.OneWay);

    public string PlayerName
    {
        get => (string)GetValue(PlayerNameProperty);
        set => SetValue(PlayerNameProperty, value);
    }

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(MainPage), null, BindingMode.OneWay);
    
    public Connection? Connection
    {
        get => (Connection?)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }
    
    // The one and only MainPage
    public static MainPage Default { get; } = new();

    internal GameManager GameManager { get; } = new();

    private IDispatcherTimer NetworkReconnectTimer { get; }

    // internal field pointing to the programs underlying network connection
    // it is "published" to other components via the Connection property when
    // the connection becomes active
    private Connection _connection = new();

    public MainPage()
    {
        InitializeComponent();

        PlayerNameEntry.Text = SettingsManager.Default.PlayerName == string.Empty ?
            Environment.UserName : SettingsManager.Default.PlayerName;

        GameManager.BindingContext = this;
        GameManager.SetBinding(GameManager.ConnectionProperty, "Connection");

        Loaded += async (s, e) =>
        {
            _connection.OnReceiveGameStart += Connection_OnReceiveGameStart;
            await ConnectNetwork();
        };

        Unloaded += (s, e) =>
        {
            _connection.OnReceiveGameStart -= Connection_OnReceiveGameStart;
            Connection = null;
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
            await _connection.ConnectAsync();
            Connection = _connection;
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
        NetworkGameSaver.Default.SaveGame(_connection.GameId, _connection.PlayerId, _connection.Color);
        var opponentName = (_connection.Color == PlayerColor.Black ? e.GameInfo.WhiteName : e.GameInfo.BlackName) ?? throw new Exception("Opponent name is null");
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
        _connection.SetGameInfo(gameId, playerId, myColor);
        MainPageMode = MainPageMode.Wait;
        await _connection.RejoinGame();
    }
}

public class MainPageModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Enum.TryParse<MainPageMode>(parameter as string, out var mode) && mode == (MainPageMode)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One way conversion");
}