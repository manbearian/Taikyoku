using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Globalization;

using ShogiClient;
using ShogiComms;
using ShogiEngine;

namespace MauiUI;

public partial class NewGameView : ContentView
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

    public NewGameView()
    {
        InitializeComponent();

        Loaded += NewGameView_Loaded;
        Unloaded += NewGameView_Unloaded;
    }

    private void NewGameView_Loaded(object? sender, EventArgs e)
    {
        Connection.OnReceiveGameStart += Connection_OnReceiveGameStart;
    }


    private void NewGameView_Unloaded(object? sender, EventArgs e)
    {
        Connection.OnReceiveGameStart -= Connection_OnReceiveGameStart;
    }

    private void CancelBtn_Clicked(object sender, EventArgs e) =>
        MainPage.Default.MainPageMode = MainPageMode.Default;

    private async void CreateBtn_Clicked(object sender, EventArgs e)
    {
        await Connection.RequestNewGame(nameFld.Text, blackBtn.IsChecked, new TaikyokuShogi());
        MainPage.Default.MainPageMode = MainPageMode.Wait;
    }

    private void Connection_OnReceiveGameStart(object sender, ReceiveGameStartEventArgs e)
    {
        throw new NotImplementedException();
    }
}

public class EmptyStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ((string)value)?.Length > 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("One way conversion");
}
