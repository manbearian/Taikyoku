namespace MauiUI;

using Microsoft.AspNetCore.SignalR;
using ShogiClient;
using ShogiComms;

public partial class WaitView : ContentView
{
    //
    // Bindable Proprerties
    //

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(MyGamesView), null, BindingMode.OneWay);

    public Connection? Connection
    {
        get => (Connection?)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    public WaitView()
    {
        InitializeComponent();
    }

    private async void CancelBtn_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (Connection is not null)
            {
                await Connection.CancelGame();
            }
            MainPage.Default.MainPageMode = MainPageMode.Home;
        }
        catch (HubException ex) when (Connection?.IsGameNotFoundException(ex) ?? false)
        {
        }
    }
}
