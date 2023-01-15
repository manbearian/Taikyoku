using ShogiEngine;

namespace MauiUI;

public partial class InfoPanel : ContentView
{
    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty GameProperty = BindableProperty.Create(nameof(Game), typeof(TaikyokuShogi), typeof(BoardView));

    public TaikyokuShogi Game
    {
        get => (TaikyokuShogi)GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    public bool IsShown { get; private set; }

    public bool IsShowingPieceInfo { get => PieceInfoView.IsVisible; }

    public bool IsShowingSettings { get => SettingsView.IsVisible; }

    public InfoPanel()
    {
        InitializeComponent();

        Hide();
    }

    public void Hide()
    {
        // Note that 'IsVisible' isn't used because if it isn't set our view
        // isn't drawn and the animations doesn't work.
        Opacity = 0.0;
        InputTransparent = true;
        IsEnabled = false;
        IsShown = false;
    }

    public void Show()
    {
        Opacity = 1.0;
        InputTransparent = false;
        IsEnabled = true;
        IsShown = true;
    }

    public void DisplayPiece(PieceIdentity p)
    {
        SettingsView.IsVisible = false;
        PieceInfoView.IsVisible = true;
        PieceInfoView.PieceId = p;
    }

    public void DisplayOptions()
    {
        SettingsView.IsVisible = true;
        PieceInfoView.IsVisible = false;
    }

    private void CloseBtn_Clicked(object sender, EventArgs e) => Hide();
}
