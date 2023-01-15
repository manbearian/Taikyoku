using System.Diagnostics.Contracts;

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

    private string? OpponentName { get; }

    private bool IsLocalGame { get => Connection is null; }

    private BoardPanelView[] Panels { get; }

    // Create board for a local game
    public BoardPage(TaikyokuShogi game, Guid? localGameId = null) : this(game, null, null, localGameId) { }

    // Create board for a network game
    public BoardPage(TaikyokuShogi game, Connection connection, string opponentName) : this(game, connection, opponentName, null) { }

    private BoardPage(TaikyokuShogi game, Connection? connection, string? opponentName, Guid? localGameId)
    {
        InitializeComponent();

        (Connection, OpponentName, LocalGameId, Game) = (connection, opponentName, localGameId, game);

        Panels = new BoardPanelView[8] { panel0, panel1, panel2, panel3, panel4, panel5, panel6, panel7 };
        UpdateBorder();

        Loaded += BoardPage_Loaded;
        Unloaded += BoardPage_Unloaded;
        NavigatingFrom += BoardPage_NavigatingFrom;

        board.OnPlayerChange += Board_OnPlayerChange;
        board.OnSelectionChanged += Board_OnSelectionChanged;
    }

    private async void Board_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (infoPanel.IsShowingSettings)
            await HideInfoPanel();

        if (e.Piece is null)
        {
            await HideInfoPanel();
        }
        else
        {
            Contract.Assert(e.SelectedLoc != null);
            infoPanel.DisplayPiece(e.Piece.Id);
            bool renderLeft = e.SelectedLoc?.X > TaikyokuShogi.BoardWidth / 2;
            await ShowInfoPanel(renderLeft);
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
        UpdateBorder();

    private async void BackBtn_Clicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private async void OptionsBtn_Clicked(object sender, EventArgs e)
    {
        if (infoPanel.IsShowingPieceInfo)
            await HideInfoPanel();

        if (infoPanel.IsShown)
        {
            await HideInfoPanel();
        }
        else
        {
            infoPanel.DisplayOptions();
            await ShowInfoPanel(false);
        }
    }

    private async Task HideInfoPanel()
    {
        if (!infoPanel.IsShown)
            return;

        var transX = infoPanel.Width;
        bool onLeft = infoPanel.TranslationX == 0;
        await infoPanel.TranslateTo(onLeft ? -transX : Width + transX, 0);
        infoPanel.Hide();
    }

    private async Task ShowInfoPanel(bool renderLeft)
    {
        bool isLeft = infoPanel.TranslationX == 0;
        if (infoPanel.IsShown && isLeft == renderLeft)
            return;

        var transX = infoPanel.Width;
        infoPanel.TranslationX = renderLeft ? -transX : Width + transX;
        infoPanel.Show();
        await infoPanel.TranslateTo(renderLeft ? 0 : Width - transX, 0);
    }

    private void UpdateBorder()
    {
        var activePlayer = Game?.CurrentPlayer;
        StatusLbl.Text = activePlayer switch
        {
            _ when activePlayer == Connection?.Color => "Your Turn",
            _ when activePlayer == Connection?.Color.Opponent() => $"Waiting for {OpponentName} to make a move",
            PlayerColor.Black => "Black's Turn",
            PlayerColor.White => "White's Turn",
            _ => ""
        };

        foreach (var p in Panels)
        {
            p.BackgroundColor = activePlayer == PlayerColor.Black ? Colors.Black : Colors.White;
            p.TextColor = activePlayer == PlayerColor.Black ? Colors.White : Colors.Black;
            p.Invalidate();
        }
    }
}