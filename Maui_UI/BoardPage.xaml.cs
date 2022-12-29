using ShogiEngine;

namespace MauiUI;

public partial class BoardPage : ContentPage
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

    //
    // Non-Bindable Properties
    //

    public Guid GameId { get; set; }

    public BoardPage(Guid gameId, TaikyokuShogi game)
    {
        InitializeComponent();

        (GameId, Game) = (gameId, game);

        NavigatingFrom += BoardPage_NavigatingFrom;
    }

    private async void BoardPage_NavigatingFrom(object? sender, NavigatingFromEventArgs e)
    {
        if (GameId == Guid.Empty)
        {
            bool saveGame = await DisplayAlert("Save Game?", "Would you like to save this game?", "Yes", "No");
            if (saveGame)
            {
                GameId = Guid.NewGuid();
            }
        }

        if (GameId != Guid.Empty)
            MySettings.SaveGame(GameId, Game);
    }

    private async void BackBtn_Clicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private void OptionsBtn_Clicked(object sender, EventArgs e)
    {

    }
}