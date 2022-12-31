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
    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty MainPageModeProperty = BindableProperty.Create(nameof(MainPageMode), typeof(MainPageMode), typeof(MyGamesView), MainPageMode.Home, BindingMode.OneWay);

    public MainPageMode MainPageMode
    {
        get => (MainPageMode)GetValue(MainPageModeProperty);
        set => SetValue(MainPageModeProperty, value);
    }

    // The one and only MainPage
    public static MainPage Default { get; } = new MainPage();

    public Connection Connection { get; } = new Connection();

    public MainPage()
    {
        InitializeComponent();

        Appearing += async (s, e) => await Connection.ConnectAsync();
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }
 
    private void MainPage_Loaded(object? sender, EventArgs e)
    {
        Connection.OnReceiveGameStart += Connection_OnReceiveGameStart;
    }

    private void MainPage_Unloaded(object? sender, EventArgs e)
    {
        Connection.OnReceiveGameStart -= Connection_OnReceiveGameStart;
    }

    private void NewLocalGameBtn_Clicked(object sender, EventArgs e) =>
        Navigation.PushModalAsync(new BoardPage(Guid.Empty, new TaikyokuShogi()), true);

    private void NewOnlineGameBtn_Clicked(object sender, EventArgs e) =>
        MainPageMode = MainPageMode.NewNetworkGame;

    private void JoinOnlineGameBtn_Clicked(object sender, EventArgs e) =>
        MainPageMode = MainPageMode.FindNetworkGame;

    private void Connection_OnReceiveGameStart(object sender, ReceiveGameStartEventArgs e)
    {
        MySettings.NetworkGameManager.SaveGame(Connection.GameId, Connection.PlayerId, Connection.Color);
        Dispatcher.Dispatch(() =>
        {
            Navigation.PushModalAsync(new BoardPage(e.GameInfo.GameId, e.Game), true);
            MainPageMode = default;
        });
    }
}

public class MainPageModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Enum.TryParse<MainPageMode>(parameter as string, out var mode) && mode == (MainPageMode)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One way conversion");
}