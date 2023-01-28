using ShogiClient;
using ShogiEngine;
using System.Diagnostics.Contracts;

namespace MauiUI;

public partial class SettingsView : ContentView
{
    //
    // Internal Properties
    //

    private static GameManager GameManager { get => MainPage.Default.GameManager; }

    public SettingsView()
    {
        InitializeComponent();

        Loaded += (s, e) =>
        {
            Refresh();
            GameManager.OnGameChange += GameManager_OnGameChange;
        };

        Unloaded += (s, e) => GameManager.OnGameChange -= GameManager_OnGameChange;
    }

    private void GameManager_OnGameChange(object sender, GameChangeEventArgs e) =>
        Dispatcher.Dispatch(() => Refresh());

    private void RotateOption_CheckedChanged(object sender, CheckedChangedEventArgs e) =>
        SettingsManager.Default.AutoRotateBoard = e.Value;

    // Invoke when externally updating the underlying game state
    public void Refresh()
    {
        RotateOption.IsChecked = SettingsManager.Default.AutoRotateBoard;
        ResignBtn.IsEnabled = GameManager.CurrentPlayer is not null;
    }

    private async void ResignBtn_Clicked(object sender, EventArgs e)
    {
        Contract.Assert(GameManager.CurrentPlayer is not null);
        await GameManager.Resign();
        await BoardPage.Default.HideInfoPanel();
    }
}