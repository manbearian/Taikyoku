using System;
using System.Collections.Generic;
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
        readonly Dictionary<MenuItem, (Player, PieceIdentity)> pieceMenuItems = new Dictionary<MenuItem, (Player, PieceIdentity)>();

        TaiyokuShogi Game = null;
        PieceInfoWindow _pieceInfoWindow = null;

        private bool DebugMode { get; set; }

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

            foreach(var pieceId in (Enum.GetValues(typeof(PieceIdentity)) as PieceIdentity[]).OrderBy(piece => piece.Name()))
            {
                var blackMenuItem = new MenuItem();
                blackMenuItem.Header = pieceId.Name();
                var whiteMenuItem = new MenuItem();
                whiteMenuItem.Header = pieceId.Name();

                pieceMenuItems.Add(blackMenuItem, (Player.Black, pieceId));
                addBlackPieceMenuItem.Items.Add(blackMenuItem);
                pieceMenuItems.Add(whiteMenuItem, (Player.White, pieceId));
                addWhitePieceMenuItem.Items.Add(whiteMenuItem);
            }

            NewGame();
        }

        private void NewGame()
        {
            Game = new TaiyokuShogi();
            gameBoard.SetGame(Game);

            Game.OnPlayerChange += OnPlayerChange;
            Game.OnBoardChange += OnBoardChange;

            Game.Reset();
        }

        private void OnPlayerChange(object sender, PlayerChangeEventArgs eventArgs)
        {
            if (eventArgs.NewPlayer == Player.White)
            {
                corners.ForEach(corner => { corner.Fill = Brushes.White; });
                borders.ForEach(border => { border.FillColor = Brushes.White; border.TextColor = Brushes.Black; border.InvalidateVisual(); });
            }
            else if (eventArgs.NewPlayer == Player.Black)
            {
                corners.ForEach(corner => { corner.Fill = Brushes.Black; });
                borders.ForEach(border => { border.FillColor = Brushes.Black; border.TextColor = Brushes.White; border.InvalidateVisual(); });
            }
            else
            {
                throw new NotSupportedException();
            }

            InvalidateVisual();
        }

        private void OnBoardChange(object sender, BoardChangeEventArgs eventArgs)
        {
            gameBoard.InvalidateVisual();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source == newGameMenuItem)
            {
                Game.Reset();
            }
            else if (e.Source == debugModeMenuItem)
            {
                DebugMode = (e.Source as MenuItem).IsChecked;
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
                for (int i = 0; i < TaiyokuShogi.BoardHeight; ++i)
                {
                    for (int j = 0; j < TaiyokuShogi.BoardHeight; ++j)
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
            _pieceInfoWindow.SetPiece(Game, piece.Value.Id);

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
