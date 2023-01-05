using ShogiEngine;

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
        // Note that 'IsVisible' isn't used because if it is set our view isn't drawn
        // and the animations don't work.
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

        var promoId = p.PromotesTo();
        promoLabel.IsVisible = promoId is not null;
        promoTitleLabel.IsEnabled = promoId is not null;
        promoSubtitleLabel.IsVisible = promoId is not null;
        promoView.IsVisible = promoId is not null;
        if (promoId is not null)
        {
            promoTitleLabel.Text = promoId.Value.Name();
            promoSubtitleLabel.Text = $"{promoId.Value.Kanji()} ({promoId.Value.Romanji()})";
            promoView.ShowPiece(promoId.Value);
        }

        Opacity = 1.0;
        InputTransparent = false;
        IsEnabled = true;
        IsShown = true;
    }

    private void CloseBtn_Clicked(object sender, EventArgs e) => Hide();
}
