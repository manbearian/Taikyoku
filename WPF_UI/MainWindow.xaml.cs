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
using System.Reflection;

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
        string? OpponentName = null;

        private bool IsNetworkGame { get => NetworkConnection is not null; }

        private void ClearNetworkInfo() => (NetworkConnection, OpponentName) = (null, string.Empty);

        private TaikyokuShogi? Game { get => _game; }

        public MainWindow()
        {
            InitializeComponent();

#if RELEASE
            debugModeMenuItem.IsEnabled = false;
#endif

            foreach (var pieceId in (Enum.GetValues(typeof(PieceIdentity)) as PieceIdentity[]).EmptyIfNull().OrderBy(piece => piece.Name()))
            {
                var blackMenuItem = new MenuItem() { Header = pieceId.Name() };
                var whiteMenuItem = new MenuItem() { Header = pieceId.Name() };

                pieceMenuItems.Add(blackMenuItem, new Piece(PlayerColor.Black, pieceId));
                addBlackPieceMenuItem.Items.Add(blackMenuItem);
                pieceMenuItems.Add(whiteMenuItem, new Piece(PlayerColor.White, pieceId));
                addWhitePieceMenuItem.Items.Add(whiteMenuItem);
            }

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
            if (networkGameState is not null)
            {
                // todo: this prevents the main window from drawing while connection
                // is in progress. I think it might be better to draw the window first?
                var window = new ReconnectWindow(networkGameState.GameId, networkGameState.PlayerId, networkGameState.MyColor);
                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game is not null, "DialogReult true => Game != null");

                    NetworkConnection = window.Connection;
                    OpponentName = window.Opponent ?? string.Empty;

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
                Contract.Assert(NetworkConnection is not null);
                Contract.Assert(OpponentName is not null);
                Contract.Assert(NetworkConnection.GameId != Guid.Empty);
                Contract.Assert(NetworkConnection.PlayerId != Guid.Empty);

                GameSaver.RecordNetworkGame(NetworkConnection.GameId, NetworkConnection.PlayerId, NetworkConnection.Color);
                 
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
            Dispatcher.Invoke(() =>
            {
                _game = game;
                gameBoard.SetGame(_game, NetworkConnection);
                InvalidateVisual();
            });
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

            Dispatcher.Invoke(() =>
            {
                corners.ForEach(corner => { corner.Fill = fillColor; });
                borders.ForEach(border => { border.FillColor = fillColor; border.TextColor = textColor; border.InvalidateVisual(); });

                playMenu.IsEnabled = player is not null;

                if (IsNetworkGame)
                {
                    if (NetworkConnection?.Color == player)
                        StatusBarTextBlock2.Text = "Your move!";
                    else if (NetworkConnection?.Color == player?.Opponent())
                        StatusBarTextBlock2.Text = "Waiting on opponent...";
                }

                InvalidateVisual();
            });
        }

        private void SaveGame(string path)
        {
            if (Game is null)
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
            var statusText = eventArgs.Winner switch
            {
                null => "Draw!",
                PlayerColor.White when !IsNetworkGame => "White Wins!",
                PlayerColor.Black when !IsNetworkGame => "Black Wins!",
                _ when IsNetworkGame && eventArgs.Winner == NetworkConnection?.Color => "You win!",
                _ when IsNetworkGame => "You lose",
                _ => throw new NotSupportedException()
            };

            Dispatcher.Invoke(() =>
            {
                StatusBarTextBlock2.Text = statusText;
                StatusBarTextBlock2.Visibility = Visibility.Visible;

                // todo: when a completed game is loaded from disk it causes the game ending window to show up
                //       before the game window itself
                new GameEndWindow().ShowDialog(eventArgs.Ending, eventArgs.Winner, statusText);
            });
        }

        private void OnClose(object? Sender, EventArgs e)
        {
            _pieceInfoWindow?.Close();

            // save the game on exit
            if (Game is not null)
            {
                NetworkGameState? networkInfo = null;
                if (IsNetworkGame)
                {
                    Contract.Assert(NetworkConnection is not null);
                    networkInfo = new NetworkGameState(NetworkConnection.GameId, NetworkConnection.PlayerId, NetworkConnection.Color);
                }
                GameSaver.RecordGameState(Game, networkInfo);
            }
        }

        private async void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source == newGameMenuItem)
            {
                var window = new NewGameWindow();
                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game is not null, "DialogResult == true => Game != null");

                    (NetworkConnection, OpponentName) = window.NetworkGame ?
                        (window.Connection, window.OpponentName)
                        : (null, string.Empty);

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
                    KnownGames = GameSaver.GetNetworkGames().EmptyIfNull()
                };
                

                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game is not null, "DialogResult == true => Game != null");

                    NetworkConnection = window.Connection;
                    OpponentName = window.OpponentName;
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
                    Contract.Assert(window.Game is not null, "DialogResult == true => Game != null");

                    NetworkConnection = window.Connection;
                    OpponentName = window.OpponentName;
                    StartGame(window.Game);
                }
            }
            else if (e.Source == addOpponentMenuItem)
            {
                var window = new NewGameWindow() { Game = Game };
                if (window.ShowDialog() == true)
                {
                    Contract.Assert(window.Game is not null, "DialogResult == true => Game != null");

                    NetworkConnection = window.Connection;
                    OpponentName = window.OpponentName;
                    StartGame(window.Game);
                }
            }
            else if (e.Source == closeMenuItem)
            {
                Close();
            }
            else if (e.Source == resignMenuItem)
            {
                if (IsNetworkGame)
                {
                    Contract.Assert(NetworkConnection is not null);
                    await NetworkConnection.ResignGame();
                }
                else
                {
                    Game?.Resign(NetworkConnection?.Color ?? Game.CurrentPlayer ?? throw new NotSupportedException());
                    OnPlayerChange(this, new PlayerChangeEventArgs(null, null));
                    OnGameEnd(this, new GameEndEventArgs(Game?.Ending ?? throw new NotSupportedException(), Game.Winner));
                }
            }
            else if (e.Source == rotateMenuItem)
            {
                gameBoard.IsRotated = rotateMenuItem.IsChecked;
                Settings.Default.RotateBoard = rotateMenuItem.IsChecked;
                Settings.Default.Save();
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
            Contract.Assert(e.GameId == NetworkConnection?.GameId);

            // update our saved games
            ChangeGame(e.Game);
        }

        void OnReceiveGameDisconnect(object sender, ReceiveGameConnectionEventArgs e)
        {
            Contract.Assert(e.GameId == NetworkConnection?.GameId);

            Dispatcher.Invoke(() => StatusBarTextBlock1.Text = $"vs. {OpponentName} (opponent disconnected)");
        }

        void OnReceiveGameReconnect(object sender, ReceiveGameConnectionEventArgs e)
        {
            Contract.Assert(e.GameId == NetworkConnection?.GameId);

            Dispatcher.Invoke(() => StatusBarTextBlock1.Text = $"vs. {OpponentName}");
        }

        void ShowPieceInfo(object sender, MouseEventArgs e)
        {
            if (Game is null)
                return;

            if (e.Source is MenuItem)
                return;

            var loc = gameBoard.GetBoardLoc(e.GetPosition(gameBoard));

            if (loc is null)
            {
                _pieceInfoWindow?.Hide();
                return;
            }

            var piece = Game.GetPiece(loc.Value);

            if (piece is null)
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
