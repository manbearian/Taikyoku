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
        (Connection Connection, Guid GameId, Guid PlayerId, Player? LocalPlayer)? _networkInfo = null;

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
            Guid networkGameId = Guid.Empty;
            Guid playerId = Guid.Empty;

            try
            {
                (savedGame, networkGameId, playerId) = GameSaver.Load(Properties.Settings.Default.SavedGame);
            }
            catch (System.Text.Json.JsonException)
            {
                // silently ignore failure to parse the game
            }

            if (networkGameId == Guid.Empty)
            {
                StartGame(savedGame ?? new TaikyokuShogi());
            }
            else
            {
                Contract.Assume(playerId != Guid.Empty, "networkGameId != null => localPlayer.Game != null");

                // todo: this prevents the main window from drawing while connection
                // is in progress. I think it might be better to draw the window first?
                var window = new ReconnectWindow(networkGameId, playerId);
                if (window.ShowDialog() == true)
                {
                    Contract.Assume(window.Game != null, "DialogReult true => Game != null");

                    StartGame(window.Game, (window.Connection, window.GameId, window.PlayerId, window.LocalPlayer));
                }
                else
                {
                    // failed to reconnect network game, create a new game
                    MessageBox.Show("Failed to reconnect network game.", "Network Game", MessageBoxButton.OK, MessageBoxImage.Error);
                    StartGame(new TaikyokuShogi());
                }
            }
        }

        private void StartGame(TaikyokuShogi game, (Connection Connection, Guid GameId, Guid PlayerId, Player? LocalPlayer)? networkInfo = null)
        {
            if (networkInfo == null)
            {
                StatusBarTextBlock1.Text = "Local Game";
                StatusBarTextBlock2.Text = "";
                StatusBarSeparator2.Visibility = Visibility.Hidden;
                StatusBarTextBlock2.Visibility = Visibility.Hidden;
            }
            else
            {
                // todo: there's a race condition here as the other player could make a move and even disconnect before we set this event handler
                //       perhaps we should poll the state after setting this up.
                networkInfo.Value.Connection.OnReceiveGameUpdate += OnReceiveUpdate;
                networkInfo.Value.Connection.OnReceiveGameDisconnect += OnReceiveGameDisconnect;
                networkInfo.Value.Connection.OnReceiveGameReconnect += OnReceiveGameReconnect;

                StatusBarTextBlock1.Text = "Network Game";
                StatusBarTextBlock2.Text = "";
                StatusBarSeparator2.Visibility = Visibility.Visible;
                StatusBarTextBlock2.Visibility = Visibility.Visible;
            }

            _networkInfo = networkInfo;
            UpdateGame(game);
        }

        private void UpdateGame(TaikyokuShogi game)
        {
            _game = game;
            gameBoard.SetGame(_game, _networkInfo);
            InvalidateVisual();
        }

        private void SetPlayer(Player? player)
        {
            var (fillColor, textColor) = player switch
            {
                Player.White => (Brushes.White, Brushes.Black),
                Player.Black => (Brushes.Black, Brushes.White),
                null => (Brushes.Gray, Brushes.Black),
                _ => throw new InvalidOperationException()
            };

            corners.ForEach(corner => { corner.Fill = fillColor; });
            borders.ForEach(border => { border.FillColor = fillColor; border.TextColor = textColor; border.InvalidateVisual(); });

            if (_networkInfo?.LocalPlayer == player)
                StatusBarTextBlock2.Text = "Your move!";
            else if (_networkInfo?.LocalPlayer == player?.Opponent())
                StatusBarTextBlock2.Text = "Waiting on opponent...";

            InvalidateVisual();
        }

        private void SaveGame(string path)
        {
            if (Game == null)
                return;

            using var stream = File.OpenWrite(path);
            stream.Write(GameSaver.Save(Game, _networkInfo?.GameId ?? Guid.Empty, _networkInfo?.PlayerId ?? Guid.Empty));
        }

        private void LoadGame(string path)
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[stream.Length];
            stream.Read(buffer);

            try
            {
                var (game, _, _) = GameSaver.Load(buffer);
                StartGame(game);
            }
            catch (System.Text.Json.JsonException)
            {
                MessageBox.Show("Cannot open save game file. It is either corrupt, invalid, or incorrect version.", "File Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPlayerChange(object sender, PlayerChangeEventArgs eventArgs) =>
            SetPlayer(eventArgs.NewPlayer);

        private void OnGameEnd(object sender, GameEndEventArgs eventArgs) =>
            new GameEndWindow().ShowDialog(eventArgs.Ending, eventArgs.Winner);

        private void OnClose(object? Sender, EventArgs e)
        {
            _pieceInfoWindow?.Close();

            // save the game, and all other settings, on exit
            if (Game != null)
            {
                Properties.Settings.Default.SavedGame = GameSaver.Save(Game, _networkInfo?.GameId ?? Guid.Empty, _networkInfo?.PlayerId ?? Guid.Empty);
                Properties.Settings.Default.Save();
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source == newGameMenuItem)
            {
                var window = new NewGameWindow();
                if (window.ShowDialog() == true)
                {
                    Contract.Assume(window.Game != null, "DialogResult == true => Game !+ null");

                    if (window.NetworkGame)
                        StartGame(window.Game, (window.Connection, window.GameId, window.PlayerId, window.LocalPlayer));
                    else
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
                    _networkInfo = null;
                    LoadGame(loadDialog.FileName);
                }
            }
            else if (e.Source == myGamesMenuItem)
            {
                var window = new KnownGamesWindow();
                if (window.ShowDialog() == true)
                {
                }
            }
            else if (e.Source == connectMenuItem)
            {
                var window = new ConnectionWindow();
                if (window.ShowDialog() == true)
                {
                    Contract.Assume(window.Game != null, "DialogResult == true => Game !+ null");
                    StartGame(window.Game, (window.Connection, window.GameId, window.PlayerId, window.LocalPlayer));
                }
            }
            else if (e.Source == addOpponentMenuItem)
            {
                var window = new NewGameWindow() { Game = Game };
                if (window.ShowDialog() == true)
                {
                    Contract.Assume(window.Game != null, "DialogResult == true => Game !+ null");
                    StartGame(window.Game, (window.Connection, window.GameId, window.PlayerId, window.LocalPlayer));
                }
            }
            else if (e.Source == closeMenuItem)
            {
                Close();
            }
            else if (e.Source == rotateMenuItem)
            {
                gameBoard.IsRotated = rotateMenuItem.IsChecked;
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
            if (_networkInfo == null || e.GameId != _networkInfo.Value.GameId)
                return;

            UpdateGame(e.Game);
        }

        void OnReceiveGameDisconnect(object sender, ReceiveGameConnectionEventArgs e)
        {
            // if we've disconnected our game ignore the update
            if (_networkInfo == null || e.GameId != _networkInfo.Value.GameId)
                return;

            StatusBarTextBlock1.Text = "Network Game (opponent disconnected)";
        }

        void OnReceiveGameReconnect(object sender, ReceiveGameConnectionEventArgs e)
        {
            // if we've disconnected our game ignore the update
            if (_networkInfo == null || e.GameId != _networkInfo.Value.GameId)
                return;

            StatusBarTextBlock1.Text = "Network Game";
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
