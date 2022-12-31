using System.Globalization;

using ShogiClient;
using ShogiEngine;

namespace MauiUI;

public enum MainPageMode
{
    Default, NewNetworkGame, JoinNetworkGame, Wait
}

public partial class MainPage : ContentPage
{
    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(MyGamesView));

    public Connection Connection
    {
        get => (Connection)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    public static readonly BindableProperty MainPageModeProperty = BindableProperty.Create(nameof(MainPageMode), typeof(MainPageMode), typeof(MyGamesView), MainPageMode.Default, BindingMode.OneWay);

    public MainPageMode MainPageMode
    {
        get => (MainPageMode)GetValue(MainPageModeProperty);
        set => SetValue(MainPageModeProperty, value);
    }

    // The one and only MainPage
    public static MainPage Default { get; } = new MainPage();

    public MainPage()
    {
        InitializeComponent();

        Connection = new Connection();

        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object? sender, EventArgs e)
    {
        await Connection.ConnectAsync();
    }

    private void NewLocalGameBtn_Clicked(object sender, EventArgs e)
    {
        Navigation.PushModalAsync(new BoardPage(Guid.Empty, new TaikyokuShogi()), true);
    }

    private void NewOnlineGameBtn_Clicked(object sender, EventArgs e)
    {
        MainPageMode = MainPageMode.NewNetworkGame;
    }
}

public class MainPageModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Enum.TryParse<MainPageMode>(parameter as string, out var mode) && mode == (MainPageMode)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One way conversion");
}