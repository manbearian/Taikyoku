﻿
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using ShogiEngine;
using ShogiClient;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for board.xaml
    /// </summary>
    public partial class Board : UserControl
    {
        private TaikyokuShogi? Game;

        public int BoardWidth { get => TaikyokuShogi.BoardWidth; }

        public int BoardHeight { get => TaikyokuShogi.BoardHeight; }

        public double SpaceWidth { get => ActualWidth / BoardWidth; }

        public double SpaceHeight { get => ActualHeight / BoardHeight; }

        public (int X, int Y)? Selected { get; set; }

        public (int X, int Y)? Selected2 { get; set; }

        private bool _isRotated = false;

        private Connection? _networkConnection = null;

        // debug functionality
        private Piece? _addingPiece = null;
        private bool _removingPiece = false;

        public Piece? AddingPiece
        {
            get => _addingPiece;
            set
            {
                Cursor = value is null ? Cursors.Arrow : Cursors.Cross;
                _addingPiece = value;
                _removingPiece = false;
            }
        }

        public bool RemovingPiece
        {
            get => _removingPiece;
            set
            {
                Cursor = value ? Cursors.Cross : Cursors.Arrow;
                _removingPiece = value;
                _addingPiece = null;
            }
        }

        public Board()
        {
            InitializeComponent();

            MouseLeftButtonUp += LeftClickHandler;
            MouseRightButtonUp += RightClickHandler;
        }

        public void SetGame(TaikyokuShogi game, Connection? networkConnection = null)
        {
            Game = game;
            _networkConnection = networkConnection;

            Selected = null;
            Selected2 = null;

            Dispatcher.Invoke(() =>
            {
                IsEnabled = Game.CurrentPlayer is not null && (networkConnection is null || Game.CurrentPlayer == networkConnection.Color);
                IsRotated = networkConnection?.Color == PlayerColor.White;
                InvalidateVisual();
            });

            OnPlayerChange?.Invoke(this, new PlayerChangeEventArgs(null, Game.CurrentPlayer));

            // check if we loaded up a game that has ended
            if (Game.Ending is not null)
            {
                OnGameEnd?.Invoke(this, new GameEndEventArgs(Game.Ending.Value, Game.Winner));
            }
        }

        public void ClearBoard()
        {
            Dispatcher.Invoke(() =>
            {
                Game?.Debug_RemoveAllPieces();

                Selected = null;
                Selected2 = null;

                InvalidateVisual();
            });
        }

        public (int X, int Y)? GetBoardLoc(Point p)
        {
            // check for negative values before rounding
            if (p.X < 0 || p.Y < 0)
                return null;

            var (x, y) = _isRotated ?
                (BoardWidth - 1 - (int)(p.X / SpaceWidth), BoardHeight - 1 - (int)(p.Y / SpaceHeight))
                    : ((int)(p.X / SpaceWidth), (int)(p.Y / SpaceHeight));

            return (x < 0 || x >= BoardWidth || y < 0 || y >= BoardHeight) ? null : (x, y);
        }

        public Rect BoardLocToRect((int X, int Y) loc) => new (loc.X * SpaceWidth, loc.Y * SpaceHeight, SpaceWidth, SpaceHeight);

        private void RightClickHandler(object sender, MouseButtonEventArgs e)
        {
            Selected = null;
            Selected2 = null;

            InvalidateVisual();
            e.Handled = true;
        }

        private void LeftClickHandler(object sender, MouseButtonEventArgs e)
        {
            if (Game is null)
                return;

            var loc = GetBoardLoc(e.GetPosition(this));

            if (loc is null)
                return;

            var clickedPiece = Game.GetPiece(loc.Value);

            if (AddingPiece is not null)
            {
                if (clickedPiece is null)
                {
                    Game.Debug_SetPiece(AddingPiece, loc.Value);
                }

                AddingPiece = null;
            }
            else if (RemovingPiece)
            {
                if (clickedPiece is not null)
                {
                    Game.Debug_SetPiece(null, loc.Value);
                }

                if (Selected == loc || Selected2 == loc)
                {
                    Selected = null;
                    Selected2 = null;
                }

                RemovingPiece = false;
            }
            else if (Selected is null)
            {
                Selected = clickedPiece is not null ? loc : null;
                Selected2 = null;
            }
            else
            {
                var selectedPiece = Game.GetKnownPiece(Selected.Value);

                if (selectedPiece.Owner == Game.CurrentPlayer)
                {
                    if (Selected2 is null
                        && Game.GetLegalMoves(selectedPiece, Selected.Value).Where(move => move.Loc == loc.Value).Any(move => move.Type == MoveType.Igui || move.Type == MoveType.Area))
                    {
                        Selected2 = loc;
                    }
                    else
                    {
                        var legalMoves = Game.GetLegalMoves(selectedPiece, Selected.Value, Selected2).Where(move => move.Loc == loc.Value);
                        if (legalMoves.Any())
                        {
                            bool promote = legalMoves.Any(move => move.Promotion == PromotionType.Must);

                            if (!promote && legalMoves.Any(move => move.Promotion == PromotionType.May))
                            {
                                // must ask the player...
                                var x = new PromotionWindow();
                                promote = x.ShowDialog(Game, selectedPiece.Id, selectedPiece.Id.PromotesTo() ?? throw new Exception());
                            }

                            MakeMove(Selected.Value, loc.Value, Selected2, promote);

                            Selected = null;
                            Selected2 = null;
                        }
                        else
                        {
                            Selected = clickedPiece is not null ? loc : null;
                            Selected2 = null;
                        }
                    }
                }
                else
                {
                    Selected = clickedPiece is not null ? loc : null;
                    Selected2 = null;
                }
            }

            InvalidateVisual();
            e.Handled = true;

            void MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null, bool promote = false)
            {
                if (_networkConnection is not null)
                {
                    Task.Run(() => _networkConnection.RequestMove(startLoc, endLoc, midLoc, promote));
                    IsEnabled = false;
                    return;
                }

                PlayerColor prevPlayer = Game.CurrentPlayer.Value;

                Game.MakeMove(startLoc, endLoc, midLoc, promote);
 
                if (Game.Ending is not null)
                {
                    IsEnabled = false;

                    InvalidateVisual();

                    OnGameEnd?.Invoke(this, new GameEndEventArgs(Game.Ending.Value, Game.Winner));
                }

                if (prevPlayer != Game.CurrentPlayer)
                    OnPlayerChange?.Invoke(this, new PlayerChangeEventArgs(prevPlayer, Game.CurrentPlayer));
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            // we need at least 1 px to draw the grid
            if (ActualWidth == 0 || ActualHeight == 0)
                return;

            if (Game is not null)
            {
                dc.PushTransform(new RotateTransform(_isRotated ? 180 : 0, ActualWidth / 2, ActualHeight / 2));

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
                Geometry ComputePieceGeometry()
                {
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

                    PathFigure f = new PathFigure(upperLeft, new List<PathSegment>()
                    {
                        new LineSegment(upperMid, true),       //  /
                        new LineSegment(upperRight, true),     //    \
                        new LineSegment(lowerRight, true),     //    |
                        new LineSegment(lowerLeft, true),      //    _
                        new LineSegment(upperLeft, true),      // |
                    }, true);

                    return new PathGeometry(new List<PathFigure>() { f });
                }

                // Compute geometry once as all the pieces are identical
                var pieceGeometry = ComputePieceGeometry();

                for (int i = 0; i < BoardWidth; ++i)
                {
                    for (int j = 0; j < BoardHeight; ++j)
                    {
                        var piece = Game.GetPiece((i, j));

                        if (piece is not null)
                        {
                            DrawPiece(dc, pieceGeometry, (i, j), piece);
                        }
                    }
                }
            }

            void DrawPiece(DrawingContext dc, Geometry pieceGeometry, (int X, int Y) loc, Piece piece)
            {
                dc.PushTransform(new TranslateTransform(SpaceWidth * loc.X, SpaceHeight * loc.Y));
                dc.PushTransform(new RotateTransform(piece.Owner == PlayerColor.White ? 180 : 0, SpaceWidth / 2, SpaceHeight / 2));

                var brush = (loc == Selected) ? ((piece.Owner == Game.CurrentPlayer) ? Brushes.Blue : Brushes.Red) : Brushes.Black;
                var pen = new Pen(brush, 1.0);
                dc.DrawGeometry(Brushes.SandyBrown, pen, pieceGeometry);

                var chars = piece.Kanji.EnumerateRunes();
                var verticalKanji = string.Join("\n", chars);
                var size = chars.Count() == 1 ? SpaceHeight * 0.5 : SpaceHeight * 0.33;
                var pieceText = new FormattedText(
                    verticalKanji,
                    CultureInfo.GetCultureInfo("jp-jp"),
                    FlowDirection.LeftToRight,
                    new Typeface("MS Gothic"),
                    size,
                    piece.Promoted ? Brushes.Gold : Brushes.Black,
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

                if (Selected is not null)
                {
                    var loc = Selected.Value;
                    var selectedPiece = Game.GetKnownPiece(loc);

                    var moves = Game.GetLegalMoves(selectedPiece, loc);

                    // first color in the basic background for movable squares
                    foreach (var move in moves)
                    {
                        var outlineBrush = Brushes.Transparent;
                        if (Game.GetPiece(move.Loc) is not null)
                            outlineBrush = selectedPiece.Owner == Game.CurrentPlayer ? Brushes.Blue : Brushes.Red;
                        dc.DrawRectangle(selectedPiece.Owner == Game.CurrentPlayer ? Brushes.LightBlue : Brushes.Pink, new Pen(outlineBrush, 1.0), GetRect(move.Loc));
                    }

                    if (Selected2 is not null)
                    {
                        var secondMoves = Game.GetLegalMoves(selectedPiece, loc, Selected2.Value);

                        // Color in the location for secondary moves
                        foreach (var move in secondMoves)
                        {
                            var outlineBrush = Brushes.Transparent;
                            var captureOwner = Game.GetPiece(move.Loc)?.Owner ?? Game.CurrentPlayer;
                            if (captureOwner != Game.CurrentPlayer)
                                outlineBrush = selectedPiece.Owner == Game.CurrentPlayer ? Brushes.Blue : Brushes.Red;
                            dc.DrawRectangle(Brushes.LightGreen, new Pen(outlineBrush, 1.0), GetRect(move.Loc));
                        }

                        // outline the Selected square
                        dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Green, 1.0), GetRect(Selected2.Value));
                    }
                }
            }
        }

        public delegate void PlayerChangeHandler(object sender, PlayerChangeEventArgs e);
        public event PlayerChangeHandler? OnPlayerChange;

        public delegate void GameEndHandler(object sender, GameEndEventArgs e);
        public event GameEndHandler? OnGameEnd;

        // Xaml Properteries

        public static readonly DependencyProperty IsRotatedProperty =
           DependencyProperty.Register("IsRotated", typeof(bool), typeof(Board), new
              PropertyMetadata(false, new PropertyChangedCallback(OnIsRotatedChanged)));

        public bool IsRotated
        {
            get => (bool)GetValue(IsRotatedProperty);
            set => SetValue(IsRotatedProperty, value);
        }

        private static void OnIsRotatedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
             (d as Board)?.OnIsRotatedChanged(e);

        private void OnIsRotatedChanged(DependencyPropertyChangedEventArgs e)
        {
            _isRotated = (bool)e.NewValue;
            InvalidateVisual();
        }
    }

    public class PlayerChangeEventArgs : EventArgs
    {
        public PlayerColor? OldPlayer { get; }

        public PlayerColor? NewPlayer { get; }

        public PlayerChangeEventArgs(PlayerColor? oldPlayer, PlayerColor? newPlayer) => (OldPlayer, NewPlayer) = (oldPlayer, newPlayer);
    }

    public class GameEndEventArgs : EventArgs
    {
        public GameEndType Ending { get; }

        public PlayerColor? Winner { get; }

        public GameEndEventArgs(GameEndType gameEnding, PlayerColor? winner) => (Ending, Winner) = (gameEnding, winner);
    }

}
