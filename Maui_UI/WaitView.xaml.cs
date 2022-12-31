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
        catch (HubException ex) when (ex.Message == string.Format(HubExceptions.OpenGameNotFound, MainPage.Default.Connection.GameId))
        {
            // TODO: This usually happens i the server already started the game... not sure what to do in this case as the oppponent probably thinks you're playing...
            // i need to ignore this cancel some how :(
        }

        MainPage.Default.MainPageMode = MainPageMode.Default;
    }
}
