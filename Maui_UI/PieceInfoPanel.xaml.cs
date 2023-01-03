using ShogiEngine;
using System.Runtime.CompilerServices;

namespace MauiUI;

public partial class PieceInfoPanel : ContentView
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
    
    public PieceInfoPanel()
    {
        InitializeComponent();

        Hide();
    }

    public void Hide()
    {
        Opacity = 0.0;
        InputTransparent = true;
        IsEnabled = false;
        IsShown = false;
    }

    public void Show(PieceIdentity p)
    {
        moveView.ShowPiece(p);
        titleLabel.Text = p.Name();
        subtitleLabel.Text = $"{p.Kanji()} ({p.Romanji()})";

        // TOOD: show more info

        Opacity = 1.0;
        InputTransparent = false;
        IsEnabled = true;
        IsShown = true;
    }

    private void CloseBtn_Clicked(object sender, EventArgs e) => Hide();
}
