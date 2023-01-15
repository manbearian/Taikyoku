namespace MauiUI;

public partial class SettingsView : ContentView
{
    public SettingsView()
    {
        InitializeComponent();

        RotateOption.IsChecked = SettingsManager.Default.AutoRotateBoard;
    }

    private void RotateOption_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        SettingsManager.Default.AutoRotateBoard = e.Value;
    }
}