
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

        public (int X, int Y)? Selected2 { get; set; }

        public bool DisplayFlipped { get => false; } //  get => Game.CurrentPlayer == Player.Black;  <-- this was disorienting, disabled

        public Board()
        {
            InitializeComponent();
        }

        public void SetGame(TaiyokuShogi game) => Game = game;

        public (int X, int Y)? GetBoardLoc(Point p)
        {
            // check for negative values before rounding
            if (p.X < 0 || p.Y < 0)
                return null;

            var (x, y) =  DisplayFlipped ?
                (BoardWidth - 1 - (int)(p.X / SpaceWidth), BoardHeight - 1 - (int)(p.Y / SpaceHeight))
                    : ((int)(p.X / SpaceWidth), (int)(p.Y / SpaceHeight));

            return (x < 0 || x >= BoardWidth || y < 0 || y >= BoardHeight) ? null as (int,int)? : (x, y);
        }

        public Rect BoardLocToRect((int X, int Y) loc) => new Rect(loc.X * SpaceWidth, loc.Y * SpaceHeight, SpaceWidth, SpaceHeight);

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            Selected = null;
            Selected2 = null;

            InvalidateVisual();
            e.Handled = true;
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            var loc = GetBoardLoc(e.GetPosition(this));

            if (loc == null)
                return;

            var clickedPiece = Game.GetPiece(loc.Value);

            if (Selected == null)
            {
                Selected = clickedPiece != null ? loc : null;
                Selected2 = null;
            }
            else
            {
                var selectedPiece = Game.GetPiece(Selected.Value).Value;

                if (selectedPiece.Owner == Game.CurrentPlayer)
                {
                    if (Selected2 == null
                        &&  Game.GetLegalMoves(selectedPiece.Owner, selectedPiece.Id, Selected.Value).Where(move => move.Loc == loc.Value).Any(move => move.Type == MoveType.Igui || move.Type == MoveType.Area))
                    {
                        Selected2 = loc;
                    }
                    else if (Game.GetLegalMoves(selectedPiece.Owner, selectedPiece.Id, Selected.Value, Selected2).Any(move => move.Loc == loc.Value))
                    {
                        Game.MakeMove(Selected.Value, loc.Value, Selected2);
                        Selected = null;
                        Selected2 = null;
                    }
                    else
                    {
                        Selected = clickedPiece != null ? loc : null;
                        Selected2 = null;
                    }
                }
                else
                {
                    Selected = clickedPiece != null ? loc : null;
                    Selected2 = null;
                }
            }

            InvalidateVisual();
            e.Handled = true;
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnRender(DrawingContext dc)
        {
            // we need at least 1 px to draw the grid
            if (ActualWidth == 0 || ActualHeight== 0)
                return;

            if (Game != null)
            {
                // flipping the board on turn exchange is disorienting
                dc.PushTransform(new RotateTransform(DisplayFlipped ? 180 : 0, ActualWidth / 2, ActualHeight / 2));

                // draw the background
                dc.DrawRectangle(Brushes.AntiqueWhite, null, new Rect(0, 0, ActualWidth, ActualHeight));

                DrawBoard(dc);
                DrawMoves(dc);
                DrawPieces(dc);
                dc.Pop();
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
                        var piece = Game.GetPiece((i, j));

                        if (piece != null)
                        {
                            DrawPiece(dc, (i, j), piece.Value.Id, piece.Value.Owner);
                        }
                    }
                }
            }

            void DrawPiece(DrawingContext dc, (int X, int Y) loc, PieceIdentity id, Player owner)
            {
                dc.PushTransform(new TranslateTransform(SpaceWidth * loc.X, SpaceHeight * loc.Y));
                dc.PushTransform(new RotateTransform(owner == Player.Black ? 180 : 0, SpaceWidth / 2, SpaceHeight / 2));

#if true
                var border = SpaceWidth * 0.05; // 5% border

                var pieceWidth = (SpaceWidth - (2 * border)) * 0.7; // narrow piece
                var pieceHeight = (SpaceHeight - (2 * border));

                var upperWidth = pieceWidth * 0.2;
                var upperHeight = pieceHeight * 0.3;

                var upperLeft = new Point((pieceWidth - upperWidth) / 2, upperHeight);
                var upperRight = new Point(SpaceWidth - upperLeft.X, upperHeight);
                var upperMid = new Point(SpaceWidth / 2, border);

                var lowerLeft = new Point((SpaceWidth - pieceWidth) / 2, SpaceHeight - border);
                var lowerRight = new Point(SpaceWidth - lowerLeft.X, SpaceHeight - border);

                var brush = (loc == Selected) ? ((owner == Game.CurrentPlayer) ? Brushes.Blue : Brushes.Red) : Brushes.Black;
                var pen = new Pen(brush, 1.0);
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
                var chars = Pieces.Kanji(id).EnumerateRunes();
                var verticalKanji = string.Join("\n", chars);
                var size = chars.Count() == 1 ? SpaceHeight * 0.5 : SpaceHeight * 0.33;
                var pieceText = new FormattedText(
                    verticalKanji,
                    CultureInfo.GetCultureInfo("jp-jp"),
                    FlowDirection.LeftToRight,
                    new Typeface("MS Gothic"),
                    size,
                    Brushes.Black,
                    1.25);

                dc.DrawText(pieceText, new Point(SpaceWidth / 2 - pieceText.Width / 2, SpaceHeight * 0.2));

                dc.Pop();
                dc.Pop();
            }

            void DrawMoves(DrawingContext dc)
            {
                Rect GetRect((int X, int Y) loc)
                {
                    var rect = BoardLocToRect(loc);
                    rect.Location = new Point(rect.X + 1, rect.Y + 1);
                    rect.Height -= 2;
                    rect.Width -= 2;
                    return rect;
                }

                if (Selected != null)
                {
                    var loc = Selected.Value;
                    var (selectedOwner, selectedId) = Game.GetPiece(loc).Value;

                    var moves = Game.GetLegalMoves(selectedOwner, selectedId, loc);

                    // first color in the basic background for movable squares
                    foreach (var move in moves)
                    {
                        var outlineBrush = Brushes.Transparent;
                        if (Game.GetPiece(move.Loc) != null)
                            outlineBrush = selectedOwner == Game.CurrentPlayer ? Brushes.Blue : Brushes.Red;
                        dc.DrawRectangle(selectedOwner == Game.CurrentPlayer ? Brushes.LightBlue : Brushes.Pink, new Pen(outlineBrush, 1.0), GetRect(move.Loc));
                    }

                    if (Selected2 != null)
                    {
                        var secondMoves = Game.GetLegalMoves(selectedOwner, selectedId, loc, Selected2.Value);

                        // Color in the location for secondary moves
                        foreach (var move in secondMoves)
                        {
                            var outlineBrush = Brushes.Transparent;
                            var (captureOwner, _) = Game.GetPiece(move.Loc) ?? (Game.CurrentPlayer, 0);
                            if (captureOwner != Game.CurrentPlayer)
                                outlineBrush = selectedOwner == Game.CurrentPlayer ? Brushes.Blue : Brushes.Red;
                            dc.DrawRectangle(Brushes.LightGreen, new Pen(outlineBrush, 1.0), GetRect(move.Loc));
                        }

                        // outline the Selected square
                        dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Green, 1.0), GetRect(Selected2.Value));
                    }
                }
            }
        }
    }
}
