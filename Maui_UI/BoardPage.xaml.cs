using ShogiClient;
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

    private Guid? LocalGameId { get;  }

    private Connection? Connection { get; }

    private bool IsLocalGame { get => Connection is null; }

    // Create board for a local game
    public BoardPage(TaikyokuShogi game, Guid? localGameId = null) : this(game, null, localGameId) { }

    // Create board for a network game
    public BoardPage(TaikyokuShogi game, Connection connection) : this(game, connection, null) { }

    private BoardPage(TaikyokuShogi game, Connection? connection, Guid? localGameId)
    {
        InitializeComponent();

        (Connection, LocalGameId, Game) = (connection, localGameId, game);

        Loaded += BoardPage_Loaded;
        Unloaded += BoardPage_Unloaded;
        NavigatingFrom += BoardPage_NavigatingFrom;
    }

    private void BoardPage_Loaded(object? sender, EventArgs e)
    {
        MainPage.Default.Connection.OnReceiveGameUpdate += Connection_OnReceiveGameUpdate;
    }

    private void BoardPage_Unloaded(object? sender, EventArgs e)
    {
        MainPage.Default.Connection.OnReceiveGameUpdate -= Connection_OnReceiveGameUpdate;
    }

    private void Connection_OnReceiveGameUpdate(object sender, ShogiClient.ReceiveGameUpdateEventArgs e)
    {
        Game = e.Game;
    }
 
    private async void BoardPage_NavigatingFrom(object? sender, NavigatingFromEventArgs e)
    {
        if (IsLocalGame)
        {
            var saveGameId = LocalGameId;
            if (saveGameId == null && !Game.BoardStateEquals(new TaikyokuShogi()))
            {
                bool saveGame = await DisplayAlert("Save Game?", "Would you like to save this game?", "Yes", "No");
                if (saveGame)
                {
                    saveGameId = Guid.NewGuid();
                }
            }
            if (saveGameId is not null)
                MySettings.LocalGameManager.SaveGame(saveGameId.Value, Game);
        }
    }

    private async void BackBtn_Clicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    // TODO: implement this???
    private async void OptionsBtn_Clicked(object sender, EventArgs e) =>
        await DisplayAlert("NYI", "Not yet implemented", "OK");
}