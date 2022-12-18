using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using ShogiEngine;
using ShogiClient;

using WPF_UI.Properties;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly List<Shape> corners = new List<Shape>();
        readonly List<NumberPanel> borders = new List<NumberPanel>();
        readonly Dictionary<MenuItem, Piece> pieceMenuItems = new Dictionary<MenuItem, Piece>();

        TaikyokuShogi? _game = null;
        PieceInfoWindow? _pieceInfoWindow = null;
        Connection? NetworkConnection = null;
        PlayerColor? LocalPlayer = null;
        string? OpponentName = null;
        Guid GameId = Guid.Empty;
        Guid PlayerId = Guid.Empty;

        private bool IsNetworkGame { get => GameId != Guid.Empty; }

        private void ClearNetworkInfo()
        {
            NetworkConnection = null;
            LocalPlayer = null;
            OpponentName = null;
            GameId = Guid.Empty;
            PlayerId = Guid.Empty;
        }

        private TaikyokuShogi? Game { get => _game; }

        public MainWindow()
        {
            InitializeComponent();

#if !DEBUG
            debugModeMenuItem.IsEnabled = false;

            foreach (var pieceId in (Enum.GetValues(typeof(PieceIdentity)) as PieceIdentity[]).OrderBy(piece => piece.Name()))
            {
                var blackMenuItem = new MenuItem() { Header = pieceId.Name() };
                var whiteMenuItem = new MenuItem() { Header = pieceId.Name() };

                pieceMenuItems.Add(blackMenuItem, new Piece(Player.Black, pieceId));
                addBlackPieceMenuItem.Items.Add(blackMenuItem);
                pieceMenuItems.Add(whiteMenuItem, new Piece(Player.White, pieceId));
                addWhitePieceMenuItem.Items.Add(whiteMenuItem);
            }
#endif

            gameBoard.IsRotated = Properties.Settings.Default.RotateBoard;

            MouseMove += ShowPieceInfo;
            Closed += OnClose;

            gameBoard.OnPlayerChange += OnPlayerChange;
            gameBoard.OnGameEnd += OnGameEnd;

            corners.Add(borderTopLeft);
            corners.Add(borderTopRight);
            corners.Add(borderBottomLeft);
            corners.Add(borderBottomRight);

            borders.Add(borderTop);
            borders.Add(borderBottom);
            borders.Add(borderLeft);
            borders.Add(borderRight);

            TaikyokuShogi? savedGame = null;
            NetworkGameState? networkGameState = null;

            try
            {
                (savedGame, networkGameState) = GameSaver.LoadMostRecentGame();
            }
            catch (System.Text.Json.JsonException)
            {
                // silently ignore failure to parse the game
            }

            // reconnect to the server for network games
            if (networkGameState != null)
            {
                // todo: this prevents the main window from drawing while connection
                // is in progress. I think it might be better to draw the window first?
                var window = new ReconnectWindow(networkGameState.GameId, networkGameState.PlayerId, networkGameState.MyColor);
                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game != null, "DialogReult true => Game != null");

                    NetworkConnection = window.Connection;
                    LocalPlayer = window.LocalPlayer;
                    OpponentName = window.Opponent;
                    GameId = window.GameId;
                    PlayerId = window.PlayerId;

                    savedGame = window.Game;
                }
                else
                {
                    // failed to reconnect network game, create a new game
                    MessageBox.Show("Failed to reconnect network game.", "Network Game", MessageBoxButton.OK, MessageBoxImage.Error);
                    GameSaver.ClearMostRecentGame();
                }
            }

            StartGame(savedGame ?? new TaikyokuShogi());
        }

        private void StartGame(TaikyokuShogi game)
        {
            if (IsNetworkGame)
            {
                Contract.Assert(NetworkConnection != null);
                Contract.Assert(LocalPlayer != null);
                Contract.Assert(OpponentName != null);
                Contract.Assert(GameId != Guid.Empty);
                Contract.Assert(PlayerId != Guid.Empty);

                GameSaver.RecordNetworkGame(GameId, PlayerId, LocalPlayer ?? throw new NullReferenceException());
                 
                // todo: there's a race condition here as the other player could make a move and even disconnect before we set this event handler
                //       perhaps we should poll the state after setting this up.
                NetworkConnection.OnReceiveGameUpdate += OnReceiveUpdate;
                NetworkConnection.OnReceiveGameDisconnect += OnReceiveGameDisconnect;
                NetworkConnection.OnReceiveGameReconnect += OnReceiveGameReconnect;

                StatusBarTextBlock1.Text = $"vs. {OpponentName}";
                StatusBarTextBlock2.Text = "";
                StatusBarSeparator2.Visibility = Visibility.Visible;
                StatusBarTextBlock2.Visibility = Visibility.Visible;

                rotateMenuItem.IsEnabled = false;
            }
            else
            {
                StatusBarTextBlock1.Text = "Local Game";
                StatusBarTextBlock2.Text = "";
                StatusBarSeparator2.Visibility = Visibility.Hidden;
                StatusBarTextBlock2.Visibility = Visibility.Hidden;

                rotateMenuItem.IsEnabled = true;
            }

            ChangeGame(game);
        }

        private void ChangeGame(TaikyokuShogi game)
        {
            _game = game;
            gameBoard.SetGame(_game, NetworkConnection, LocalPlayer);
            InvalidateVisual();
        }

        private void SetPlayer(PlayerColor? player)
        {
            var (fillColor, textColor) = player switch
            {
                PlayerColor.White => (Brushes.White, Brushes.Black),
                PlayerColor.Black => (Brushes.Black, Brushes.White),
                null => (Brushes.Gray, Brushes.Black),
                _ => throw new InvalidOperationException()
            };

            corners.ForEach(corner => { corner.Fill = fillColor; });
            borders.ForEach(border => { border.FillColor = fillColor; border.TextColor = textColor; border.InvalidateVisual(); });

            playMenu.IsEnabled = player != null;

            if (IsNetworkGame)
            {
                Contract.Assert(LocalPlayer != null);

                if (LocalPlayer == player)
                    StatusBarTextBlock2.Text = "Your move!";
                else if (LocalPlayer == player?.Opponent())
                    StatusBarTextBlock2.Text = "Waiting on opponent...";
            }

            InvalidateVisual();
        }

        private void SaveGame(string path)
        {
            if (Game == null)
                return;

            using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(Game.Serialize());
        }

        private void LoadGame(string path)
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[stream.Length];
            stream.Read(buffer);

            try
            {
                StartGame(TaikyokuShogi.Deserlialize(buffer));
            }
            catch (System.Text.Json.JsonException)
            {
                MessageBox.Show("Cannot open save game file. It is either corrupt, invalid, or incorrect version.", "File Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPlayerChange(object sender, PlayerChangeEventArgs eventArgs) =>
            SetPlayer(eventArgs.NewPlayer);

        private void OnGameEnd(object sender, GameEndEventArgs eventArgs)
        {
            if (IsNetworkGame)
            {
                StatusBarTextBlock2.Text = (eventArgs.Winner == null) ?
                    "Draw!" : ((eventArgs.Winner == LocalPlayer) ? "You win!" : "You lost");
            }
            else
            {
                StatusBarTextBlock2.Text = eventArgs.Winner switch
                {
                    PlayerColor.White => "White Wins!",
                    PlayerColor.Black => "Black Wins!",
                    null => "Draw!",
                    _ => throw new NotSupportedException()
                };
            }

            StatusBarTextBlock2.Visibility = Visibility.Visible;

            // todo: when a completed game is loaded from disk it causes the game ending window to show up
            //       before the game window itself
            new GameEndWindow().ShowDialog(eventArgs.Ending, eventArgs.Winner);
        }

        private void OnClose(object? Sender, EventArgs e)
        {
            _pieceInfoWindow?.Close();

            // save the game on exit
            if (Game != null)
            {
                GameSaver.RecordGameState(Game, IsNetworkGame ? new NetworkGameState(GameId, PlayerId, LocalPlayer ?? throw new Exception()) : null);
            }
        }

        private async void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source == newGameMenuItem)
            {
                var window = new NewGameWindow();
                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game != null, "DialogResult == true => Game != null");

                    (NetworkConnection, LocalPlayer, GameId, PlayerId, OpponentName) = window.NetworkGame ?
                        (window.Connection, window.LocalPlayer, window.GameId, window.PlayerId, window.OpponentName)
                        : (null, null, Guid.Empty, Guid.Empty, null);

                    StartGame(window.Game);
                }
            }
            else if (e.Source == saveGameMenuItem)
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog()
                {
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    DefaultExt = "shogi",
                    FileName = "save",
                    Filter = "Shogi files (*.shogi)|*.shogi|All files (*.*)|*.*"
                };

                if (saveDialog.ShowDialog() ?? false)
                {
                    SaveGame(saveDialog.FileName);
                }
            }
            else if (e.Source == loadGameMenuItem)
            {
                var loadDialog = new Microsoft.Win32.OpenFileDialog()
                {
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    Multiselect = false,
                    DefaultExt = "shogi",
                    FileName = "",
                    Filter = "Shogi files (*.shogi)|*.shogi|All files (*.*)|*.*"
                };

                if (loadDialog.ShowDialog() ?? false)
                {
                    ClearNetworkInfo();
                    LoadGame(loadDialog.FileName);
                }
            }
            else if (e.Source == myGamesMenuItem)
            {
                var window = new ConnectionWindow()
                {
                    KnownGames = GameSaver.GetNetworkGames().EmptyIfNull().Select(elem => (elem.GameId, elem.PlayerId, elem.MyColor))
                };
                

                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game != null, "DialogResult == true => Game != null");

                    NetworkConnection = window.Connection;
                    LocalPlayer = window.LocalPlayer;
                    OpponentName = window.OpponentName;
                    GameId = window.GameId;
                    PlayerId = window.PlayerId;
                    StartGame(window.Game);
                }

                // remove games the server was unaware of from our known game list
                foreach (var game in window.DeadGames)
                {
                    GameSaver.RemoveNetworkGame(game.GameId, game.PlayerId);
                }
            }
            else if (e.Source == connectMenuItem)
            {
                var window = new ConnectionWindow();
                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game != null, "DialogResult == true => Game != null");

                    NetworkConnection = window.Connection;
                    LocalPlayer = window.LocalPlayer;
                    OpponentName = window.OpponentName;
                    GameId = window.GameId;
                    PlayerId = window.PlayerId;
                    StartGame(window.Game);
                }
            }
            else if (e.Source == addOpponentMenuItem)
            {
                var window = new NewGameWindow() { Game = Game };
                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game != null, "DialogResult == true => Game != null");

                    NetworkConnection = window.Connection;
                    LocalPlayer = window.LocalPlayer;
                    OpponentName = window.OpponentName;
                    GameId = window.GameId;
                    PlayerId = window.PlayerId;
                    StartGame(window.Game);
                }
            }
            else if (e.Source == closeMenuItem)
            {
                Close();
            }
            else if (e.Source == resignMenuItem)
            {
                Game?.Resign(LocalPlayer ?? Game.CurrentPlayer ?? throw new NotSupportedException());
                if (IsNetworkGame)
                {
                    Contract.Assert(NetworkConnection != null);
                    await NetworkConnection.RequestResign();
                }
                gameBoard.InvalidateVisual();
                OnPlayerChange(this, new PlayerChangeEventArgs(null, null));
                OnGameEnd(this, new GameEndEventArgs(Game?.Ending ?? throw new NotSupportedException(), Game.Winner));
            }
            else if (e.Source == rotateMenuItem)
            {
                gameBoard.IsRotated = rotateMenuItem.IsChecked;
                Properties.Settings.Default.RotateBoard = rotateMenuItem.IsChecked;
                Properties.Settings.Default.Save();
            }
            else if (e.Source == debugModeMenuItem)
            {
                // nothing to do... "Debug" state is tracked through the "checked" property of this menu item
            }
            else if (pieceMenuItems.TryGetValue((MenuItem)e.Source, out var piece))
            {
                gameBoard.AddingPiece = piece;
            }
            else if (e.Source == removePieceMenuItem)
            {
                gameBoard.RemovingPiece = true;
            }
            else if (e.Source == clearBoardMenuItem)
            {
                gameBoard.ClearBoard();
            }
            else if (e.Source == switchTurnMenuItem)
            {
                Game?.Debug_EndTurn();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        void OnReceiveUpdate(object sender, ReceiveGameUpdateEventArgs e)
        {
            // if we've disconnected our game ignore the update
            if (e.GameId != GameId)
                return;

            // update our saved games
            ChangeGame(e.Game);
        }

        void OnReceiveGameDisconnect(object sender, ReceiveGameConnectionEventArgs e)
        {
            // if we've disconnected our game ignore the update
            if (e.GameId != GameId)
                return;

            Dispatcher.Invoke(() => StatusBarTextBlock1.Text = $"vs. {OpponentName} (opponent disconnected)");
        }

        void OnReceiveGameReconnect(object sender, ReceiveGameConnectionEventArgs e)
        {
            // if we've disconnected our game ignore the update
            if (e.GameId != GameId)
                return;

            Dispatcher.Invoke(() => StatusBarTextBlock1.Text = $"vs. {OpponentName}");
        }

        void ShowPieceInfo(object sender, MouseEventArgs e)
        {
            if (Game == null)
                return;

            if (e.Source is MenuItem)
                return;

            var loc = gameBoard.GetBoardLoc(e.GetPosition(gameBoard));

            if (loc == null)
            {
                _pieceInfoWindow?.Hide();
                return;
            }

            var piece = Game.GetPiece(loc.Value);

            if (piece == null)
            {
                _pieceInfoWindow?.Hide();
                return;
            }

            var mainWindow = Application.Current.MainWindow;
            var pos = e.GetPosition(mainWindow);

            _pieceInfoWindow ??= new PieceInfoWindow();
            _pieceInfoWindow.SetPiece(Game, piece.Id);

            if (mainWindow.WindowState == WindowState.Normal)
            {
                _pieceInfoWindow.Left = pos.X + mainWindow.Left + 15;
                _pieceInfoWindow.Top = pos.Y + mainWindow.Top + 30;
            }
            else
            {
                // maximized

                _pieceInfoWindow.Left = pos.X + 15;
                _pieceInfoWindow.Top = pos.Y + 30;

                if (_pieceInfoWindow.Left > mainWindow.ActualWidth - (_pieceInfoWindow.Width * 2))
                {
                    _pieceInfoWindow.Left = pos.X - _pieceInfoWindow.Width;
                }

                if (_pieceInfoWindow.Top > mainWindow.ActualHeight - (_pieceInfoWindow.Height * 2))
                {
                    _pieceInfoWindow.Top = pos.Y - _pieceInfoWindow.Height;
                }
            }

            _pieceInfoWindow.Show();
        }
    }
}
