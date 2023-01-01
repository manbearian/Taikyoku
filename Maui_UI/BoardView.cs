using ShogiClient;
using ShogiEngine;

namespace MauiUI;

internal class BoardDrawer : IDrawable
{
    private BoardView View { get; set; }

    public BoardDrawer(BoardView view) =>
        View = view;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var width = View.CenterWidth;
        var height = View.CenterHeight;
        var fullWidth = (float)View.Width;
        var fullHeight = (float)View.Height;
        var boardWidth = View.BoardWidth;
        var boardHeight = View.BoardHeight;
        var spaceWidth = View.SpaceWidth;
        var spaceHeight = View.SpaceHeight;
        var game = View.Game;

        // we need at least 1 px to draw the grid
        if (width == 0 || height == 0)
            return;

        if (game is null)
            return;

        canvas.Rotate(View.IsRotated ? 180 : 0, width / 2, height / 2);

        // Draw background
        canvas.FillColor = Colors.AntiqueWhite;
        canvas.FillRectangle(0, 0, fullWidth, fullHeight);

        if (View.IsLabeled)
        {
            var labelWidth = View.LabelWidth;
            var labelHeight = View.LabelHeight;

            canvas.FillColor = Colors.White;
            canvas.FillRectangle(0, 0, fullWidth, labelHeight);
            canvas.FillRectangle(0, 0, labelWidth, fullHeight);
            canvas.FillRectangle(0, fullHeight - labelHeight, fullWidth, labelHeight);
            canvas.FillRectangle(fullWidth - labelWidth, 0, labelWidth, fullHeight);

            canvas.FontColor = Colors.Black;
            canvas.FontSize = labelHeight;
            var spacer = labelHeight * 0.7f;

            for (int i = 0; i < boardWidth; ++i)
            {
                canvas.DrawString($"{boardWidth - i}", i * spaceWidth + (spaceWidth / 2) + labelWidth, spacer, HorizontalAlignment.Center);
                canvas.DrawString($"{boardWidth - i}", i * spaceWidth + (spaceWidth / 2) + labelWidth, fullHeight - labelHeight + spacer, HorizontalAlignment.Center);
            }

            static string RowName(int i) => new((char)('A' + (i % 26)), i / 26 + 1);

            for (int i = 0; i < boardHeight; ++i)
            {
                canvas.DrawString(RowName(i), labelWidth / 2, (i * spaceHeight) + (spaceHeight / 2) + labelHeight + spacer / 2, HorizontalAlignment.Center);
                canvas.DrawString(RowName(i), labelWidth / 2 + fullWidth - labelWidth, (i * spaceHeight) + (spaceHeight / 2) + labelHeight + spacer / 2, HorizontalAlignment.Center);
            }

            canvas.Translate(labelWidth, labelHeight);
        }

        DrawBoard(canvas);
        DrawMoves(canvas);
        DrawPieces(canvas);

        canvas.ResetState();

        void DrawBoard(ICanvas canvas)
        {
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 1.0f;

            for (int i = 0; i < boardWidth + 1; ++i)
            {
                canvas.DrawLine(new Point(width / boardWidth * i, 0.0), new Point(width / boardWidth * i, height));
            }

            for (int i = 0; i < boardHeight + 1; ++i)
            {
                canvas.DrawLine(new Point(0.0, height / boardHeight * i), new Point(width, height / boardHeight * i));
            }
        }

        void DrawPieces(ICanvas canvas)
        {
            PathF ComputePieceGeometry()
            {
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

                var path = new PathF();
                path.MoveTo(upperLeft);
                path.LineTo(upperMid);        //  /
                path.LineTo(upperRight);      //    \
                path.LineTo(lowerRight);      //    |
                path.LineTo(lowerLeft);       //  __
                path.LineTo(upperLeft);       // |
                return path;
            }

            // Compute geometry once as all the pieces are identical
            var pieceGeometry = ComputePieceGeometry();

            for (int i = 0; i < boardWidth; ++i)
            {
                for (int j = 0; j < boardHeight; ++j)
                {
                    var piece = game.GetPiece((i, j));

                    if (piece is not null)
                    {
                        DrawPiece(canvas, pieceGeometry, (i, j), piece);
                    }
                }
            }

            void DrawPiece(ICanvas canvas, PathF pieceGeometry, (int X, int Y) loc, Piece piece)
            {
                canvas.SaveState();
                canvas.Translate(spaceWidth * loc.X, spaceHeight * loc.Y);
                canvas.Rotate(piece.Owner == PlayerColor.White ? 180 : 0, spaceWidth / 2, spaceHeight / 2);

                canvas.StrokeColor = (loc == View.SelectedLoc) ? ((piece.Owner == game.CurrentPlayer) ? Colors.Blue : Colors.Red) : Colors.Black;
                canvas.StrokeSize = (loc == View.SelectedLoc) ? 3.0f : 1.0f;
                canvas.DrawPath(pieceGeometry);
                canvas.FillColor = Colors.SandyBrown;
                canvas.FillPath(pieceGeometry);

                var chars = piece.Kanji.EnumerateRunes();
                var verticalKanji = string.Join("\n", chars);
                canvas.FontSize = spaceHeight * 0.33f;
                canvas.Font = new Microsoft.Maui.Graphics.Font("MS Gothic");
                canvas.FontColor = piece.Promoted ? Colors.Gold : Colors.Black;
                canvas.DrawString(verticalKanji, spaceWidth / 2, spaceHeight / 2, HorizontalAlignment.Center);

                canvas.RestoreState();
            }
        }

        void DrawMoves(ICanvas canvas)
        {
            Rect BoardLocToRect((int X, int Y) loc) =>
                new(loc.X * spaceWidth, loc.Y * spaceHeight, spaceWidth, spaceHeight);

            Rect GetRect((int X, int Y) loc)
            {
                var rect = BoardLocToRect(loc);
                rect.Location = new Point(rect.X + 1, rect.Y + 1);
                rect.Height -= 2;
                rect.Width -= 2;
                return rect;
            }

            if (View.SelectedLoc is null)
                return;

            var loc = View.SelectedLoc.Value;
            var selectedPiece = game.GetPiece(loc);

            if (selectedPiece is null)
                return;

            var moves = game.GetLegalMoves(selectedPiece, loc);

            // first color in the basic background for movable squares
            foreach (var move in moves)
            {
                var rect = GetRect(move.Loc);
                canvas.FillColor = selectedPiece.Owner == game.CurrentPlayer ? Colors.LightBlue : Colors.Pink;
                canvas.FillRectangle(rect);

                if (game.GetPiece(move.Loc) is not null)
                {
                    canvas.StrokeColor = selectedPiece.Owner == game.CurrentPlayer ? Colors.Blue : Colors.Red;
                    canvas.StrokeSize = 1.0f;
                    canvas.DrawRectangle(rect);
                }
            }

            if (View.SelectedLoc2 is null)
                return;

            var secondMoves = game.GetLegalMoves(selectedPiece, loc, View.SelectedLoc2.Value);

            // Color in the location for secondary moves
            foreach (var move in secondMoves)
            {
                var rect = GetRect(move.Loc);
                var captureOwner = game.GetPiece(move.Loc)?.Owner ?? game.CurrentPlayer;

                canvas.FillColor = Colors.LightGreen;
                canvas.FillRectangle(rect);

                if (captureOwner != game.CurrentPlayer)
                {
                    canvas.StrokeColor = selectedPiece.Owner == game.CurrentPlayer ? Colors.Blue : Colors.Red;
                    canvas.StrokeSize = 1.0f;
                    canvas.DrawRectangle(rect);
                }
            }

            // outline the Selected square
            canvas.StrokeColor = Colors.Green;
            canvas.StrokeSize = 1.0f;
            canvas.DrawRectangle(GetRect(View.SelectedLoc2.Value));
        }
    }
}

public class BoardView : GraphicsView
{
    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty GameProperty = BindableProperty.Create(nameof(Game), typeof(TaikyokuShogi), typeof(BoardView));

    public TaikyokuShogi Game
    {
        get => (TaikyokuShogi)GetValue(GameProperty);
        set => SetValue(GameProperty, value);
    }

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(BoardView));

    public Connection? Connection
    {
        get => (Connection?)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    public static readonly BindableProperty IsRotatedProperty = BindableProperty.Create(nameof(IsRotated), typeof(bool), typeof(BoardView));

    public bool IsRotated
    {
        get => (bool)GetValue(IsRotatedProperty);
        set => SetValue(IsRotatedProperty, value);
    }

    public static readonly BindableProperty IsLabeledProperty = BindableProperty.Create(nameof(IsLabeled), typeof(bool), typeof(BoardView));

    public bool IsLabeled
    {
        get => (bool)GetValue(IsLabeledProperty);
        set => SetValue(IsLabeledProperty, value);
    }

    //
    // Standard Properties
    //

    public int BoardWidth { get => TaikyokuShogi.BoardWidth; }

    public int BoardHeight { get => TaikyokuShogi.BoardHeight; }

    public float SpaceWidth { get => CenterWidth / BoardWidth; }

    public float SpaceHeight { get => CenterHeight / BoardHeight; }

    public (int X, int Y)? SelectedLoc { get; set; } = null;
    public (int X, int Y)? SelectedLoc2 { get; set; } = null;

    private PointF? _lastTouchPoint = null;

    public float LabelWidth { get => 12.0f; }

    public float LabelHeight { get => 10.0f; }

    public float CenterWidth { get => IsLabeled ? (float)Width - 2 * LabelWidth : (float)Width; }

    public float CenterHeight { get => IsLabeled ? (float)Height - 2 * LabelHeight : (float)Height; }

    public BoardView()
    {
        // Enable custom draw
        Drawable = new BoardDrawer(this);

        // Enable tap interactions
        var tapRecognizer = new TapGestureRecognizer();
        tapRecognizer.Tapped += TapRecognizer_Tapped;
        GestureRecognizers.Add(tapRecognizer);
        EndInteraction += BoardView_EndInteraction;

        PropertyChanged += BoardView_PropertyChanged;
    }

    private void BoardView_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Game))
        {
            IsEnabled = Game.CurrentPlayer is not null && (Connection is null || Game.CurrentPlayer == Connection.Color);
            //IsRotated = Connection?.Color == PlayerColor.White;
            Invalidate();
        }
    }

    // Translate a point on the View to a space on the board
    public (int X, int Y)? GetBoardLoc(Point p)
    {
        // compensate for adornments
        p = new(p.X - LabelWidth, p.Y - LabelHeight);

        // check for negative values before rounding
        if (p.X < 0 || p.Y < 0)
            return null;

        var (x, y) = IsRotated ?
            (BoardWidth - 1 - (int)(p.X / SpaceWidth), BoardHeight - 1 - (int)(p.Y / SpaceHeight))
                : ((int)(p.X / SpaceWidth), (int)(p.Y / SpaceHeight));

        return (x < 0 || x >= BoardWidth || y < 0 || y >= BoardHeight) ? null : (x, y);
    }

    private async void TapRecognizer_Tapped(object? sender, EventArgs e)
    {
        if (_lastTouchPoint is null)
            return;

        if (Game is null)
            return;

        var loc = GetBoardLoc(_lastTouchPoint.Value);

        if (loc is null)
            return;

        var clickedPiece = Game.GetPiece(loc.Value);

        if (SelectedLoc is null)
        {
            SelectedLoc = clickedPiece is null ? null : loc;
            SelectedLoc2 = null;
        }
        else
        {
            var selectedPiece = Game.GetPiece(SelectedLoc.Value);

            if (selectedPiece is not null && selectedPiece.Owner == Game.CurrentPlayer)
            {
                if (SelectedLoc2 is null
                    && Game.GetLegalMoves(selectedPiece, SelectedLoc.Value).Where(move => move.Loc == loc.Value).Any(move => move.Type == MoveType.Igui || move.Type == MoveType.Area))
                {
                    SelectedLoc2 = loc;
                }
                else
                {
                    var legalMoves = Game.GetLegalMoves(selectedPiece, SelectedLoc.Value, SelectedLoc2).Where(move => move.Loc == loc.Value);
                    if (legalMoves.Any())
                    {
                        bool promote = legalMoves.Any(move => move.Promotion == PromotionType.Must);

                        if (!promote && legalMoves.Any(move => move.Promotion == PromotionType.May))
                        {
                            // TODO: ASK THE PLAYER
                            // must ask the player...
                            //    var x = new PromotionWindow();
                            //    promote = x.ShowDialog(Game, selectedPiece.Id, selectedPiece.Id.PromotesTo() ?? throw new Exception());
                        }

                        await MakeMove(SelectedLoc.Value, loc.Value, SelectedLoc2, promote);

                        SelectedLoc = null;
                        SelectedLoc2 = null;
                    }
                    else
                    {
                        SelectedLoc = clickedPiece is null ? null : loc;
                        SelectedLoc2 = null;
                    }
                }
            }
            else
            {
                SelectedLoc = clickedPiece is null ? null : loc;
                SelectedLoc2 = null;
            }
        }

        Invalidate();

        async Task MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null, bool promote = false)
        {
            if (Connection is not null)
            {
                await Connection.RequestMove(startLoc, endLoc, midLoc, promote);
                IsEnabled = false;
                return;
            }

            PlayerColor prevPlayer = Game.CurrentPlayer.Value;

            Game.MakeMove(startLoc, endLoc, midLoc, promote);

            if (Game.Ending is not null)
            {
                IsEnabled = false;

                Invalidate();

                // TOOD: UPDATE UI EVENTS
                //OnGameEnd?.Invoke(this, new GameEndEventArgs(Game.Ending.Value, Game.Winner));
            }

            // TOOD: UPDATE UI EVENTS
            //if (prevPlayer != Game.CurrentPlayer)
            //    OnPlayerChange?.Invoke(this, new PlayerChangeEventArgs(prevPlayer, Game.CurrentPlayer));
        }
    }

    private void BoardView_EndInteraction(object? sender, TouchEventArgs e) =>
         _lastTouchPoint = e.IsInsideBounds ? e.Touches.FirstOrDefault() : null;
}
