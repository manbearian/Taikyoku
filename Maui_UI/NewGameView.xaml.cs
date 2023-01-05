using System.Globalization;

using ShogiClient;
using ShogiEngine;

namespace MauiUI;

public partial class NewGameView : ContentView
{
    public NewGameView()
    {
        InitializeComponent();
    }

    private void CancelBtn_Clicked(object sender, EventArgs e) =>
        MainPage.Default.MainPageMode = MainPageMode.Home;

    private async void CreateBtn_Clicked(object sender, EventArgs e)
    {
        try
        {
            await MainPage.Default.Connection.RequestNewGame(nameFld.Text, blackBtn.IsChecked, new TaikyokuShogi());
            MainPage.Default.MainPageMode = MainPageMode.Wait;
        }
        catch(Exception ex) when (Connection.ExceptionFilter(ex))
        {
            await MainPage.Default.DisplayAlert("Game Creation Failed", "Unable to create a new game due to a network or server error.", "Okay");
        }
    }
}

public class EmptyStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ((string)value)?.Length > 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One way conversion");
}
