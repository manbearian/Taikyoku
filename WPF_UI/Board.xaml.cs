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

        public Board()
        {
            InitializeComponent();
        }

        public void SetGame(TaiyokuShogi game) => Game = game;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var pos = e.GetPosition(this);

            int x = (int)(pos.X / SpaceWidth);
            int y = (int)(pos.Y / SpaceHeight);

            ToolTip = null;
            var piece = Game.GetPiece(x, y);
            if (piece != null)
            {
                ToolTip =
                    $"{Pieces.Name(piece.Value.Id)}\n" + 
                    $"{Pieces.Kanji(piece.Value.Id)} ({Pieces.Romanji(piece.Value.Id)})";
            }

            base.OnMouseMove(e);
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (Game != null)
            {
                DrawBoard(dc);
                DrawPieces(dc);
            }

            base.OnRender(dc);
        }

        private void DrawBoard(DrawingContext dc)
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

        private void DrawPieces(DrawingContext dc)
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

        private void DrawPiece(DrawingContext dc, (int X, int Y) loc, PieceIdentity id, bool rotate)
        {
            var pen = new Pen(new SolidColorBrush(Colors.Black), 0.5);

            var spaceWidth = (ActualWidth / BoardWidth);
            var spaceHeight = (ActualHeight / BoardHeight);

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

            dc.PushTransform(new TranslateTransform(spaceWidth * loc.X, spaceHeight * loc.Y));
            dc.PushTransform(new RotateTransform(rotate ? 180 : 0, spaceWidth / 2, spaceHeight / 2));
            dc.DrawLine(pen, upperLeft, upperMid);     //  /
            dc.DrawLine(pen, upperMid, upperRight);    //    \
            dc.DrawLine(pen, upperRight, lowerRight);  //    |
            dc.DrawLine(pen, lowerRight, lowerLeft);   //    _
            dc.DrawLine(pen, lowerLeft, upperLeft);    // |

            var verticalKanji = string.Join("\n", Pieces.Kanji(id).EnumerateRunes());

            // Create the initial formatted text string.
            var formattedText = new FormattedText(
                verticalKanji,
                CultureInfo.GetCultureInfo("jp-jp"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                spaceHeight * 0.3,
                Brushes.Black,
                1.25);


            var textStart = new Point(spaceWidth / 2 - (spaceWidth * 0.1), upperHeight - (upperHeight * 0.25));

            dc.DrawText(formattedText, textStart);

            dc.Pop();
            dc.Pop();
        }
    }
}
