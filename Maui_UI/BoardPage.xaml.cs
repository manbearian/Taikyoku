using System.Diagnostics.Contracts;

using ShogiClient;
using ShogiEngine;

namespace MauiUI;

public partial class BoardPage : ContentPage
{
    //
    // Non-Bindable Properties
    //

    private static TaikyokuShogi Game { get => MainPage.Default.Game; }

    private static Connection Connection { get => MainPage.Default.Connection; }

    private static Guid? LocalGameId { get => MainPage.Default.LocalGameId; }

    private static string? OpponentName { get => MainPage.Default.OpponentName; }

    private static bool IsLocalGame { get => MainPage.Default.IsLocalGame; }

    private static bool AutoRotateEnabled { get; set; } = SettingsManager.Default.AutoRotateBoard;

    private BoardBorderView[] Panels { get; }

    public static BoardPage Default { get; } = new BoardPage();

    // Internal constructor called by public constructors
    private BoardPage()
    {
        InitializeComponent();

        Panels = new BoardBorderView[8] { panelN, panelS, panelE, panelW, panelNE, panelNW, panelSE, panelSW };

        NavigatedTo += BoardPage_NavigatedTo;
        NavigatingFrom += BoardPage_NavigatingFrom;

        Board.OnPlayerChange += Board_OnPlayerChange;
        Board.OnSelectionChanged += Board_OnSelectionChanged;

        SettingsManager.Default.OnSettingChanged += OnSettingChanged;
    }

    private void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        if (e.SettingName == nameof(SettingsManager.AutoRotateBoard))
        {
            AutoRotateEnabled = SettingsManager.Default.AutoRotateBoard;
            UpdateBorder();
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
        UpdateBorder();
        Board.Refresh();
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
        var activePlayer = Game.CurrentPlayer;
        StatusLbl.Text = activePlayer switch
        {
            PlayerColor.Black when IsLocalGame => "Black's Turn",
            PlayerColor.White when IsLocalGame => "White's Turn",
            _ when !IsLocalGame && activePlayer == Connection.Color => "Your Turn",
            _ when !IsLocalGame && activePlayer == Connection.Color.Opponent() => $"Waiting for {OpponentName} to make a move",
            _ => ""
        };

        bool rotateBoard = (IsLocalGame && AutoRotateEnabled) || (!IsLocalGame && Connection.Color == PlayerColor.White);
        Board.IsRotated = rotateBoard;
        foreach (var panel in Panels)
            panel.IsRotated = rotateBoard;

        foreach (var p in Panels)
        {
            p.BackgroundColor = activePlayer == PlayerColor.Black ? Colors.Black : Colors.White;
            p.TextColor = activePlayer == PlayerColor.Black ? Colors.White : Colors.Black;
            p.Invalidate();
        }
    }
}