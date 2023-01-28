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
            RedrawBorder();
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

    private void BoardPage_NavigatedTo(object? sender, NavigatedToEventArgs e)
    {
        RedrawGameBoardAndBorder();
    }

    private async void BoardPage_NavigatingFrom(object? sender, NavigatingFromEventArgs e)
    {
        if (IsLocalGame)
        {
            var saveGameId = LocalGameId;
            if (saveGameId is null && !Game.BoardStateEquals(new()))
            {
                bool saveGame = await DisplayAlert("Save Game?", "Would you like to save this game?", "Yes", "No");
                if (saveGame)
                {
                    saveGameId = Guid.NewGuid();
                }
            }
            if (saveGameId is not null)
                SettingsManager.LocalGameManager.SaveGame(saveGameId.Value, Game);
        }
    }

    private void GameManager_OnGameChange(object sender, GameChangeEventArgs e) =>
        Dispatcher.Dispatch(() => RedrawGameBoardAndBorder());

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

    private async void RedrawGameBoardAndBorder()
    {
        Board.Refresh();

        // close the info panel if its open
        await HideInfoPanel();

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

        RedrawBorder();
    }

    private void RedrawBorder()
    {
        bool rotateBoard = (IsLocalGame && AutoRotateEnabled) || (!IsLocalGame && MyColor == PlayerColor.White);
        Board.IsRotated = rotateBoard;
        foreach (var panel in Panels)
            panel.IsRotated = rotateBoard;

        var activePlayer = Game.CurrentPlayer;
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
    }
}