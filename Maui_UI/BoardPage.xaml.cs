using ShogiClient;
using ShogiEngine;
using System.Diagnostics.Contracts;

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

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(BoardView));

    public Connection? Connection
    {
        get => (Connection?)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    //
    // Non-Bindable Properties
    //

    private Guid? LocalGameId { get; }

    private bool IsLocalGame { get => Connection is null; }

    private BoardPanelView[] Panels { get; }

    // Create board for a local game
    public BoardPage(TaikyokuShogi game, Guid? localGameId = null) : this(game, null, localGameId) { }

    // Create board for a network game
    public BoardPage(TaikyokuShogi game, Connection connection) : this(game, connection, null) { }

    private BoardPage(TaikyokuShogi game, Connection? connection, Guid? localGameId)
    {
        InitializeComponent();

        (Connection, LocalGameId, Game) = (connection, localGameId, game);

        Panels = new BoardPanelView[8] { panel0, panel1, panel2, panel3, panel4, panel5, panel6, panel7 };
        SetPanelColors();

        Loaded += BoardPage_Loaded;
        Unloaded += BoardPage_Unloaded;
        NavigatingFrom += BoardPage_NavigatingFrom;

        board.OnPlayerChange += Board_OnPlayerChange;
        board.OnSelectionChanged += Board_OnSelectionChanged;
    }

    private async void Board_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var transX = infoPanel.Width;
        if (e.Piece is null)
        {
            if (infoPanel.IsShown)
            {
                if (infoPanel.TranslationX == 0)
                {
                    await infoPanel.TranslateTo(-transX, 0);
                    infoPanel.Hide();
                }
                else
                {
                    await infoPanel.TranslateTo(Width + transX, 0);
                    infoPanel.Hide();
                }
            }
        }
        else
        {
            Contract.Assert(e.SelectedLoc != null);
            bool renderLeft = e.SelectedLoc?.X > TaikyokuShogi.BoardWidth / 2;
            bool isLeft = infoPanel.TranslationX == 0;
            bool wasShown = infoPanel.IsShown;
            infoPanel.Show(e.Piece.Id);
            if (!wasShown || (isLeft ^ renderLeft))
            {
                if (renderLeft)
                {
                    infoPanel.TranslationX = -transX;
                    await infoPanel.TranslateTo(0, 0);
                }
                else
                {
                    infoPanel.TranslationX = Width + transX;
                    await infoPanel.TranslateTo(Width - transX, 0);
                }
            }
        }
    }

    private void BoardPage_Loaded(object? sender, EventArgs e)
    {
        MainPage.Default.Connection.OnReceiveGameUpdate += Connection_OnReceiveGameUpdate;
    }

    private void BoardPage_Unloaded(object? sender, EventArgs e)
    {
        MainPage.Default.Connection.OnReceiveGameUpdate -= Connection_OnReceiveGameUpdate;
    }

    private void Connection_OnReceiveGameUpdate(object sender, ReceiveGameUpdateEventArgs e)
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

    private void Board_OnPlayerChange(object sender, PlayerChangeEventArgs e) =>
        SetPanelColors();

    private async void BackBtn_Clicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    // TODO: implement this???
    private async void OptionsBtn_Clicked(object sender, EventArgs e) =>
        await DisplayAlert("NYI", "Not yet implemented", "OK");

    private void SetPanelColors()
    {
        foreach (var p in Panels)
        {
            p.BackgroundColor = Game?.CurrentPlayer == PlayerColor.Black ? Colors.Black : Colors.White;
            p.TextColor = Game?.CurrentPlayer == PlayerColor.Black ? Colors.White : Colors.Black;
            p.Invalidate();
        }
    }
}