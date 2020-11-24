using System;
using System.Collections.Generic;
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

using Oracle;

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

        TaikyokuShogi Game = null;
        PieceInfoWindow _pieceInfoWindow = null;

        public MainWindow()
        {
            InitializeComponent();

            MouseMove += ShowPieceInfo;
            Closed += (object sender, EventArgs e) => _pieceInfoWindow.Close();

            corners.Add(borderTopLeft);
            corners.Add(borderTopRight);
            corners.Add(borderBottomLeft);
            corners.Add(borderBottomRight);

            borders.Add(borderTop);
            borders.Add(borderBottom);
            borders.Add(borderLeft);
            borders.Add(borderRight);

            foreach (var pieceId in (Enum.GetValues(typeof(PieceIdentity)) as PieceIdentity[]).OrderBy(piece => piece.Name()))
            {
                var blackMenuItem = new MenuItem() { Header = pieceId.Name() };
                var whiteMenuItem = new MenuItem() { Header = pieceId.Name() };

                pieceMenuItems.Add(blackMenuItem, new Piece(Player.Black, pieceId));
                addBlackPieceMenuItem.Items.Add(blackMenuItem);
                pieceMenuItems.Add(whiteMenuItem, new Piece(Player.White, pieceId));
                addWhitePieceMenuItem.Items.Add(whiteMenuItem);
            }

            NewGame();
        }

        private void NewGame(TaikyokuShogi game = null)
        {
            Game = game ?? new TaikyokuShogi();
            gameBoard.SetGame(Game);

            Game.OnPlayerChange += OnPlayerChange;
            Game.OnGameEnd += OnGameEnd;

            SetPlayer(Game.CurrentPlayer);
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

            InvalidateVisual();
        }

        private void OnPlayerChange(object sender, PlayerChangeEventArgs eventArgs) =>
            SetPlayer(eventArgs.NewPlayer);

        private void OnGameEnd(object sender, GameEndEventArgs eventArgs)
        {
            var gameEndWindow = new GameEndWindow();
            gameEndWindow.ShowDialog(eventArgs.Ending, eventArgs.Winner);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source == newGameMenuItem)
            {
                NewGame();
            }
            else if (e.Source == saveGameMenuItem)
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                using FileStream fs = File.Open($"{path}\\a.state", FileMode.OpenOrCreate);
                fs.Write(Game.Serialize());
            }
            else if (e.Source == loadGameMenuItem)
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                using FileStream fs = File.Open($"{path}\\a.state", FileMode.Open);
                var buffer = new byte[fs.Length];
                fs.Read(buffer);
                NewGame(TaikyokuShogi.Deserlialize(buffer));
            }
            else if (e.Source == debugModeMenuItem)
            {
                // nothing to do... "Debug" state is tracked through the "checked" property of this menu item
            }
            else if (pieceMenuItems.TryGetValue(e.Source as MenuItem, out var piece))
            {
                gameBoard.AddingPiece = piece;
            }
            else if (e.Source == removePieceMenuItem)
            {
                gameBoard.RemovingPiece = true;
            }
            else if (e.Source == clearBoardMenuItem)
            {
                for (int i = 0; i < TaikyokuShogi.BoardHeight; ++i)
                {
                    for (int j = 0; j < TaikyokuShogi.BoardHeight; ++j)
                    {
                        Game.Debug_SetPiece(null, (i, j));
                    }
                }
            }
            else if (e.Source == switchTurnMenuItem)
            {
                Game.Debug_EndTurn();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        void ShowPieceInfo(object sender, MouseEventArgs e)
        {
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
