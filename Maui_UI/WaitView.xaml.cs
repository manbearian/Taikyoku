namespace MauiUI;

using ShogiClient;

public partial class WaitView : ContentView
{
    public WaitView()
    {
        InitializeComponent();
    }

    private async void CancelBtn_Clicked(object sender, EventArgs e)
    {
        await MainPage.Default.Connection.CancelGame();
        MainPage.Default.MainPageMode = MainPageMode.Default;
    }
}
