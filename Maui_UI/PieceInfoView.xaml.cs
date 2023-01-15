
using ShogiEngine;
using System.Runtime.CompilerServices;

namespace MauiUI;

public partial class PieceInfoView : ContentView
{
    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty GameProperty = BindableProperty.Create(nameof(Game), typeof(TaikyokuShogi), typeof(PieceInfoView));

    public TaikyokuShogi Game
    {
        get => (TaikyokuShogi)GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    public static readonly BindableProperty PieceIdProperty = BindableProperty.Create(nameof(PieceId), typeof(PieceIdentity), typeof(PieceInfoView));

    public PieceIdentity PieceId
    {
        get => (PieceIdentity)GetValue(PieceIdProperty);
        set => SetValue(PieceIdProperty, value);
    }

    public PieceInfoView()
    {
        InitializeComponent();

        InputTransparent = true;
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(PieceId))
        {
            titleLabel.Text = PieceId.Name();
            subtitleLabel.Text = $"{PieceId.Kanji()} ({PieceId.Romanji()})";

            var promoId = PieceId.PromotesTo();
            promoLabel.IsVisible = promoId is not null;
            promoTitleLabel.IsEnabled = promoId is not null;
            promoSubtitleLabel.IsVisible = promoId is not null;
            promoView.IsVisible = promoId is not null;
            if (promoId is not null)
            {
                promoTitleLabel.Text = promoId.Value.Name();
                promoSubtitleLabel.Text = $"{promoId.Value.Kanji()} ({promoId.Value.Romanji()})";
                promoView.PieceId = promoId.Value;
            }
        }

        base.OnPropertyChanged(propertyName);
    }
}
