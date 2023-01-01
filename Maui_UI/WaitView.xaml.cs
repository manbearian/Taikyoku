namespace MauiUI;

using Microsoft.AspNetCore.SignalR;
using ShogiClient;
using ShogiComms;

public partial class WaitView : ContentView
{
    public WaitView()
    {
        InitializeComponent();
    }

    private async void CancelBtn_Clicked(object sender, EventArgs e)
    {
        try
        {
            await MainPage.Default.Connection.CancelGame();
        }
        catch (HubException ex) when (MainPage.Default.Connection.IsGameNotFoundException(ex))
        {
            // TODO: This usually happens if the server already started the game... not sure what to do in this case as the oppponent probably thinks you're playing...
            // i need to ignore this cancel some how :(
        }

        MainPage.Default.MainPageMode = MainPageMode.Home;
    }
}
