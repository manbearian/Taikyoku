namespace MauiUI;

using ShogiClient;

public partial class WaitView : ContentView
{
    //
    // Bindable Proprerties
    //

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(NewGameView));

    public Connection Connection
    {
        get => (Connection)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    public WaitView()
    {
        InitializeComponent();
    }

    private async void CancelBtn_Clicked(object sender, EventArgs e)
    {
        await Connection.CancelGame();
        MainPage.Default.MainPageMode = MainPageMode.Default;
    }
}
