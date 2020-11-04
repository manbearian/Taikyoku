using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Oracle;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for board.xaml
    /// </summary>
    public partial class Board : UserControl
    {
        private TaiyokuShogi Game;

        public int BoardWidth { get => TaiyokuShogi.BoardWidth; }
        public int BoardHeight { get => TaiyokuShogi.BoardHeight; }

        public double SpaceWidth { get => ActualWidth / BoardWidth; }

        public double SpaceHeight { get => ActualHeight / BoardHeight; }

        public (int X, int Y)? Selected { get; set; }

        public Board()
        {
            InitializeComponent();
        }

        public void SetGame(TaiyokuShogi game) => Game = game;

        public (int X, int Y) GetBoardLoc(Point p) => ((int)(p.X / SpaceWidth), (int)(p.Y / SpaceHeight));

        public Rect BoardLocToRect((int X, int Y) loc) => new Rect(loc.X * SpaceWidth, loc.Y * SpaceHeight, SpaceWidth, SpaceHeight);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var (x, y) = GetBoardLoc(e.GetPosition(this));
            var piece = Game.GetPiece(x, y);

            ToolTip = null;
            if (piece != null)
            {
                var moves = Movement.GetMovement(piece.Value.Id);

                var layoutGrid = new Grid();
                layoutGrid.MinWidth = 50.0;
                layoutGrid.MinHeight = 100.0;
                layoutGrid.RowDefinitions.Add(new RowDefinition());
                layoutGrid.RowDefinitions.Add(new RowDefinition());

                var headerText = new TextBlock();
                headerText.Text = $"{Pieces.Name(piece.Value.Id)}\n{Pieces.Kanji(piece.Value.Id)} ({Pieces.Romanji(piece.Value.Id)})";
                headerText.FontSize = 14;
                Grid.SetRow(headerText, 0);
                layoutGrid.Children.Add(headerText);

                // figure out our grid size
                int maxMoves = 1;
                for (int i = 0; i < moves.StepRange.Length; ++i)
                {
                    maxMoves = Math.Max(maxMoves, moves.StepRange[i] == Movement.FullRange ? 1 : moves.StepRange[i]);
                }

                var gridSize = maxMoves * 2 + 1;
                var grid = new Grid();
                grid.Width = gridSize * 20.0;
                grid.Height = gridSize * 20.0;
                grid.ShowGridLines = true;
                for (int i = 0; i < gridSize; ++i)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    grid.RowDefinitions.Add(new RowDefinition());
                }
                Grid.SetRow(grid, 1);
                layoutGrid.Children.Add(grid);

                // render moves in a grid
                //     | o |  
                //  ---+---+---
                //   - | ☖| -
                //  ---+---+---
                //   o |   | o
                var pieceIcon = new TextBlock();
                pieceIcon.Text = "☖";
                pieceIcon.FontSize = 14;
                pieceIcon.HorizontalAlignment = HorizontalAlignment.Center;
                pieceIcon.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(pieceIcon, gridSize / 2);
                Grid.SetRow(pieceIcon, gridSize / 2);
                grid.Children.Add(pieceIcon);

                for (int direction = 0; direction < moves.StepRange.Length; ++direction)
                {
                    for (int i = 1; i <= maxMoves; ++i)
                    {
                        var moveIcon = new TextBlock();
                        moveIcon.Text = moves.StepRange[direction] == 0 ? "" : moves.StepRange[direction] < Movement.FullRange ? "o" : GetFullRangeChar(direction);
                        moveIcon.FontSize = 12;
                        moveIcon.HorizontalAlignment = HorizontalAlignment.Center;
                        moveIcon.VerticalAlignment = VerticalAlignment.Center;
                        var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                        Grid.SetColumn(moveIcon, gridX);
                        Grid.SetRow(moveIcon, gridY);
                        grid.Children.Add(moveIcon);
                    }
                }

                ToolTip = layoutGrid;
            }

            base.OnMouseMove(e);

            static string GetFullRangeChar(int direction) =>
                direction switch
                {
                    Movement.Up => "|",
                    Movement.Down => "|",
                    Movement.Left => "─",
                    Movement.Right => "─",
                    Movement.UpLeft => "\\",
                    Movement.UpRight => "/",
                    Movement.DownLeft => "/",
                    Movement.DownRight => "\\",
                    _ => throw new InvalidOperationException()
                };

            static (int X, int Y) GetGridPos(int gridSize, int direction, int value) =>
                direction switch
                {
                    Movement.Up => (gridSize / 2, gridSize / 2 - value),
                    Movement.Down => (gridSize / 2, gridSize / 2 + value),
                    Movement.Left => (gridSize / 2 - value, gridSize / 2),
                    Movement.Right => (gridSize / 2 + value, gridSize / 2),
                    Movement.UpLeft => (gridSize / 2 - value, gridSize / 2 - value),
                    Movement.UpRight => (gridSize / 2 + value, gridSize / 2 - value),
                    Movement.DownLeft => (gridSize / 2 - value, gridSize / 2 + value),
                    Movement.DownRight => (gridSize / 2 + value, gridSize / 2 + value),
                    _ => throw new NotSupportedException()
                };
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            e.Handled = true;

            var (x, y) = GetBoardLoc(e.GetPosition(this));
            var piece = Game.GetPiece(x, y);

            if (piece != null)
            {
                Selected = (x,y);
            }

            InvalidateVisual();
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (Game != null)
            {
                DrawBoard(dc);
                DrawPieces(dc);
                DrawMoves(dc);
            }

            base.OnRender(dc);
            void DrawBoard(DrawingContext dc)
            {
                var pen = new Pen(new SolidColorBrush(Colors.Black), 1.0);

                for (int i = 0; i < BoardWidth + 1; ++i)
                {
                    dc.DrawLine(pen, new Point(ActualWidth / BoardWidth * i, 0.0), new Point(ActualWidth / BoardWidth * i, ActualHeight));
                }

                for (int i = 0; i < BoardHeight + 1; ++i)
                {
                    dc.DrawLine(pen, new Point(0.0, ActualHeight / BoardHeight * i), new Point(ActualWidth, ActualHeight / BoardHeight * i));
                }
            }

            void DrawPieces(DrawingContext dc)
            {
                for (int i = 0; i < BoardWidth; ++i)
                {
                    for (int j = 0; j < BoardHeight; ++j)
                    {
                        var piece = Game.GetPiece(i, j);

                        if (piece != null)
                        {
                            DrawPiece(dc, (i, j), piece.Value.Id, piece?.Player == Player.Black);
                        }
                    }
                }
            }

            void DrawPiece(DrawingContext dc, (int X, int Y) loc, PieceIdentity id, bool rotate)
            {
                var spaceWidth = ActualWidth / BoardWidth;
                var spaceHeight = ActualHeight / BoardHeight;

                dc.PushTransform(new TranslateTransform(spaceWidth * loc.X, spaceHeight * loc.Y));
                dc.PushTransform(new RotateTransform(rotate ? 180 : 0, spaceWidth / 2, spaceHeight / 2));

#if true
                var border = spaceWidth * 0.05; // 5% border

                var pieceWidth = (spaceWidth - (2 * border)) * 0.7; // narrow piece
                var pieceHeight = (spaceHeight - (2 * border));

                var upperWidth = pieceWidth * 0.2;
                var upperHeight = pieceHeight * 0.3;

                var upperLeft = new Point((pieceWidth - upperWidth) / 2, upperHeight);
                var upperRight = new Point(spaceWidth - upperLeft.X, upperHeight);
                var upperMid = new Point(spaceWidth / 2, border);

                var lowerLeft = new Point((spaceWidth - pieceWidth) / 2, spaceHeight - border);
                var lowerRight = new Point(spaceWidth - lowerLeft.X, spaceHeight - border);

                var pen = new Pen((loc == Selected) ? Brushes.Red : Brushes.Black, 1.0);
                dc.DrawLine(pen, upperLeft, upperMid);     //  /
                dc.DrawLine(pen, upperMid, upperRight);    //    \
                dc.DrawLine(pen, upperRight, lowerRight);  //    |
                dc.DrawLine(pen, lowerRight, lowerLeft);   //    _
                dc.DrawLine(pen, lowerLeft, upperLeft);    // |
#else

                var piece = new FormattedText(
                    "☖",
                    CultureInfo.GetCultureInfo("jp-jp"),
                    FlowDirection.LeftToRight,
                    new Typeface("MS Gothic"),
                    spaceHeight * 1.1,
                    (loc == Selected) ? Brushes.Red : Brushes.Black,
                    1.25);

                dc.DrawText(piece, new Point(spaceWidth * 0.15, -(spaceHeight * 0.05)));
#endif

                var verticalKanji = string.Join("\n", Pieces.Kanji(id).EnumerateRunes());
                var pieceText = new FormattedText(
                    verticalKanji,
                    CultureInfo.GetCultureInfo("jp-jp"),
                    FlowDirection.LeftToRight,
                    new Typeface("MS Gothic"),
                    spaceHeight * 0.33,
                    Brushes.Black,
                    1.25);

                dc.DrawText(pieceText, new Point(spaceWidth / 2 - (spaceWidth * 0.1), spaceHeight * 0.2));

                dc.Pop();
                dc.Pop();
            }

            void DrawMoves(DrawingContext dc)
            {
                if (Selected == null)
                    return;

                var loc = Selected.Value;
                var piece = Game.GetPiece(loc).Value;

                var moves = Game.GetLegalMoves(piece.Player, piece.Id, loc);

                foreach (var move in moves)
                {
                    dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Red, 2.0), BoardLocToRect(move));
                }
            }
        }
    }
}
