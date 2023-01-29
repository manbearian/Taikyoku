using System.Diagnostics.Contracts;

using ShogiClient;
using ShogiEngine;

namespace MauiUI;

public partial class BoardPage : ContentPage
{
    //
    // Private Wrapper Properties
    //

    private static PlayerColor MyColor { get => MainPage.Default.Connection.Color; }

    private static GameManager GameManager { get => MainPage.Default.GameManager; }
    
    private static TaikyokuShogi Game { get => GameManager.Game; }

    private static Guid? LocalGameId { get => GameManager.LocalGameId; }

    private static string? OpponentName { get => GameManager.OpponentName; }

    private static bool IsLocalGame { get => GameManager.IsLocalGame; }

    //
    // Non-Bindable Properties
    //

    private static bool AutoRotateEnabled { get; set; } = SettingsManager.Default.AutoRotateBoard;

    private BoardBorderView[] Panels { get; }

    public static BoardPage Default { get; } = new();

    private BoardPage()
    {
        InitializeComponent();

        Panels = new BoardBorderView[8] { panelN, panelS, panelE, panelW, panelNE, panelNW, panelSE, panelSW };

        NavigatedTo += BoardPage_NavigatedTo;
        NavigatingFrom += BoardPage_NavigatingFrom;

        Board.OnSelectionChanged += Board_OnSelectionChanged;

        GameManager.OnGameChange += GameManager_OnGameChange; ;

        SettingsManager.Default.OnSettingChanged += OnSettingChanged;
    }

    private void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        if (e.SettingName == nameof(SettingsManager.AutoRotateBoard))
        {
            AutoRotateEnabled = SettingsManager.Default.AutoRotateBoard;
            RedrawBoardAndBorder();
        }
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
            renderLeft = Board.IsRotated ? !renderLeft : renderLeft;
            await ShowInfoPanel(renderLeft);
        }
    }

    private void BoardPage_NavigatedTo(object? sender, NavigatedToEventArgs e) =>
        RedrawBoardAndBorder();

    private async void BoardPage_NavigatingFrom(object? sender, NavigatingFromEventArgs e)
    {
        if (!IsLocalGame)
            return;

        var saveGameId = LocalGameId;

        // If the game is still in progress and not yet recorded, ask the user if they want to save it
        if (saveGameId is null && Game.CurrentPlayer is not null && !Game.BoardStateEquals(new()))
        {
            bool saveGame = await DisplayAlert("Save Game?", "Would you like to save this game?", "Yes", "No");
            if (saveGame)
                saveGameId = Guid.NewGuid();
        }

        // If the game is being recorded, save the game state (and delete completed games)
        if (saveGameId is not null)
        {
            if (Game.Ending is null)
                LocalGamesManager.Default.SaveGame(saveGameId.Value, Game);
            else
                LocalGamesManager.Default.DeleteGame(saveGameId.Value);
        }
    }

    private void GameManager_OnGameChange(object sender, GameChangeEventArgs e) =>
        Dispatcher.Dispatch(() => RedrawBoardAndBorder());

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

    public async Task HideInfoPanel()
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

    private void RedrawBoardAndBorder()
    {
        var activePlayer = Game.CurrentPlayer;
        StatusLbl.Text = activePlayer switch
        {
            PlayerColor.Black when IsLocalGame => "Black's Turn",
            PlayerColor.White when IsLocalGame => "White's Turn",
            not null when !IsLocalGame && activePlayer == MyColor => "Your Turn",
            not null when !IsLocalGame && activePlayer == MyColor.Opponent() => $"Waiting for {OpponentName} to make a move",
            null when IsLocalGame && Game.Winner is not null => $"{Game.Winner} Wins!",
            null when !IsLocalGame && Game.Winner == MyColor => $"You Won!",
            null when !IsLocalGame && Game.Winner == MyColor.Opponent() => $"{OpponentName} Won!",
            _ => ""
        };

        bool rotateBoard = !IsLocalGame && MyColor == PlayerColor.White;
        if (IsLocalGame && AutoRotateEnabled)
            rotateBoard = Game.CurrentPlayer == PlayerColor.White;
        Board.IsRotated = rotateBoard;
        foreach (var panel in Panels)
            panel.IsRotated = rotateBoard;

        foreach (var p in Panels)
        {
            p.BackgroundColor = Colors.Grey;
            p.TextColor = Colors.Black;
            if (activePlayer is not null)
            {
                p.BackgroundColor = activePlayer == PlayerColor.Black ? Colors.Black : Colors.White;
                p.TextColor = activePlayer == PlayerColor.Black ? Colors.White : Colors.Black;
            }

            p.Invalidate();
        }

        Board.Refresh();
    }
}