using ShogiClient;
using ShogiEngine;

namespace MauiUI;

public class PlayerChangeEventArgs : EventArgs
{
    public PlayerColor? OldPlayer { get; }

    public PlayerColor? NewPlayer { get; }

    public PlayerChangeEventArgs(PlayerColor? oldPlayer, PlayerColor? newPlayer) => (OldPlayer, NewPlayer) = (oldPlayer, newPlayer);
}


public class SelectionChangedEventArgs : EventArgs
{
    public Piece? Piece { get; }

    public (int X, int Y)? SelectedLoc { get; }

    public SelectionChangedEventArgs((int X, int Y)? loc, Piece? piece) => (SelectedLoc, Piece) = (loc, piece);
}

public class BoardView : GraphicsView, IDrawable
{
    //
    // Events
    //

    public delegate void PlayerChangeHandler(object sender, PlayerChangeEventArgs e);
    public event PlayerChangeHandler? OnPlayerChange;

    //
    // Events
    //

    public delegate void SelectionChangedHandler(object sender, SelectionChangedEventArgs e);
    public event SelectionChangedHandler? OnSelectionChanged;


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

    //
    // Standard Properties
    //

    public int BoardWidth { get => TaikyokuShogi.BoardWidth; }

    public int BoardHeight { get => TaikyokuShogi.BoardHeight; }

    public float SpaceWidth { get => (float)Width / BoardWidth; }

    public float SpaceHeight { get => (float)Height / BoardHeight; }

    public (int X, int Y)? SelectedLoc { get; set; } = null;
    public (int X, int Y)? SelectedLoc2 { get; set; } = null;

    private PointF? _lastTouchPoint = null;

    public BoardView()
    {
        // Enable custom draw
        Drawable = this;

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

            OnPlayerChange?.Invoke(this, new PlayerChangeEventArgs(null, Game.CurrentPlayer));

            Invalidate();
        }
    }

    // Translate a point on the View to a space on the board
    public (int X, int Y)? GetBoardLoc(Point p)
    {
        // check for negative values before rounding
        if (p.X < 0 || p.Y < 0)
            return null;

        var x = (int)(p.X / SpaceWidth);
        var y = (int)(p.Y / SpaceHeight);
        return (x < 0 || x >= BoardWidth || y < 0 || y >= BoardHeight) ? null : (x, y);
    }

    private async void TapRecognizer_Tapped(object? sender, EventArgs e)
    {
        var firstLoc = SelectedLoc;
        var secondLoc = SelectedLoc2;
        (int X, int Y)? newFirstLoc = null;
        (int X, int Y)? newSecondLoc = null;

        if (_lastTouchPoint is null)
            return;

        if (Game is null)
            return;

        var loc = GetBoardLoc(_lastTouchPoint.Value);

        if (loc is null)
            return;

        var clickedPiece = Game.GetPiece(loc.Value);

        if (firstLoc is null)
        {
            newFirstLoc = clickedPiece is null ? null : loc;
        }
        else
        {
            var selectedPiece = Game.GetPiece(firstLoc.Value);

            if (selectedPiece is not null && selectedPiece.Owner == Game.CurrentPlayer)
            {
                if (secondLoc is null
                    && Game.GetLegalMoves(selectedPiece, firstLoc.Value).Where(move => move.Loc == loc.Value).Any(move => move.Type == MoveType.Igui || move.Type == MoveType.Area))
                {
                    newSecondLoc = loc;
                }
                else
                {
                    var legalMoves = Game.GetLegalMoves(selectedPiece, firstLoc.Value, secondLoc).Where(move => move.Loc == loc.Value);
                    if (legalMoves.Any())
                    {
                        bool promote = legalMoves.Any(move => move.Promotion == PromotionType.Must);

                        if (!promote && legalMoves.Any(move => move.Promotion == PromotionType.May))
                        {
                            // TOOD: show a better dialog
                            promote = await MainPage.Default.DisplayAlert("Pormotion?", "Promte this piece?", "Yes", "No");
                        }

                        await MakeMove(firstLoc.Value, loc.Value, secondLoc, promote);
                    }
                    else
                    {
                        newFirstLoc = clickedPiece is null ? null : loc;
                    }
                }
            }
            else
            {
                newFirstLoc = clickedPiece is null ? null : loc;
            }
        }

        SelectedLoc = newFirstLoc;
        SelectedLoc2 = newSecondLoc;
        OnSelectionChanged?.Invoke(this, new SelectionChangedEventArgs(newFirstLoc, newFirstLoc is null ? null : Game.GetPiece(newFirstLoc.Value)));
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

            if (prevPlayer != Game.CurrentPlayer)
                OnPlayerChange?.Invoke(this, new PlayerChangeEventArgs(prevPlayer, Game.CurrentPlayer));
        }
    }

    private void BoardView_EndInteraction(object? sender, TouchEventArgs e) =>
         _lastTouchPoint = e.IsInsideBounds ? e.Touches.FirstOrDefault() : null;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // we need at least 1 px to draw the grid
        if (Width == 0 || Height == 0)
            return;

        if (Game is null)
            return;

        // Draw background
        canvas.FillColor = Colors.AntiqueWhite;
        canvas.FillRectangle(0, 0, (float)Width, (float)Height);

        DrawBoard(canvas);
        DrawMoves(canvas);
        DrawPieces(canvas);

        canvas.ResetState();

        return;

        //
        // Helper Functions
        //

        void DrawBoard(ICanvas canvas)
        {
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 1.0f;

            for (int i = 0; i < BoardWidth + 1; ++i)
            {
                canvas.DrawLine(new Point(Width / BoardWidth * i, 0.0), new Point(Width / BoardWidth * i, Height));
            }

            for (int i = 0; i < BoardHeight + 1; ++i)
            {
                canvas.DrawLine(new Point(0.0, Height / BoardHeight * i), new Point(Width, Height / BoardHeight * i));
            }
        }

        void DrawPieces(ICanvas canvas)
        {
            PathF ComputePieceGeometry()
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

            for (int i = 0; i < BoardWidth; ++i)
            {
                for (int j = 0; j < BoardHeight; ++j)
                {
                    var piece = Game.GetPiece((i, j));

                    if (piece is not null)
                    {
                        DrawPiece(canvas, pieceGeometry, (i, j), piece);
                    }
                }
            }

            void DrawPiece(ICanvas canvas, PathF pieceGeometry, (int X, int Y) loc, Piece piece)
            {
                canvas.SaveState();
                canvas.Translate(SpaceWidth * loc.X, SpaceHeight * loc.Y);
                canvas.Rotate(piece.Owner == PlayerColor.White ? 180 : 0, SpaceWidth / 2, SpaceHeight / 2);

                canvas.StrokeColor = (loc == SelectedLoc) ? ((piece.Owner == Game.CurrentPlayer) ? Colors.Blue : Colors.Red) : Colors.Black;
                canvas.StrokeSize = (loc == SelectedLoc) ? 3.0f : 1.0f;
                canvas.DrawPath(pieceGeometry);
                canvas.FillColor = Colors.SandyBrown;
                canvas.FillPath(pieceGeometry);

                var chars = piece.Kanji.EnumerateRunes();
                var verticalKanji = string.Join("\n", chars);
                canvas.FontSize = SpaceHeight * 0.33f;
                canvas.Font = new Microsoft.Maui.Graphics.Font("MS Gothic");
                canvas.FontColor = piece.Promoted ? Colors.Gold : Colors.Black;
                canvas.DrawString(verticalKanji, SpaceWidth / 2, SpaceHeight / 2, HorizontalAlignment.Center);

                canvas.RestoreState();
            }
        }

        void DrawMoves(ICanvas canvas)
        {
            Rect BoardLocToRect((int X, int Y) loc) =>
                new(loc.X * SpaceWidth, loc.Y * SpaceHeight, SpaceWidth, SpaceHeight);

            Rect GetRect((int X, int Y) loc)
            {
                var rect = BoardLocToRect(loc);
                rect.Location = new Point(rect.X + 1, rect.Y + 1);
                rect.Height -= 2;
                rect.Width -= 2;
                return rect;
            }

            if (SelectedLoc is null)
                return;

            var loc = SelectedLoc.Value;
            var selectedPiece = Game.GetPiece(loc);

            if (selectedPiece is null)
                return;

            var moves = Game.GetLegalMoves(selectedPiece, loc);

            // first color in the basic background for movable squares
            foreach (var move in moves)
            {
                var rect = GetRect(move.Loc);
                canvas.FillColor = selectedPiece.Owner == Game.CurrentPlayer ? Colors.LightBlue : Colors.Pink;
                canvas.FillRectangle(rect);

                if (Game.GetPiece(move.Loc) is not null)
                {
                    canvas.StrokeColor = selectedPiece.Owner == Game.CurrentPlayer ? Colors.Blue : Colors.Red;
                    canvas.StrokeSize = 1.0f;
                    canvas.DrawRectangle(rect);
                }
            }

            if (SelectedLoc2 is null)
                return;

            var secondMoves = Game.GetLegalMoves(selectedPiece, loc, SelectedLoc2.Value);

            // Color in the location for secondary moves
            foreach (var move in secondMoves)
            {
                var rect = GetRect(move.Loc);
                var captureOwner = Game.GetPiece(move.Loc)?.Owner ?? Game.CurrentPlayer;

                canvas.FillColor = Colors.LightGreen;
                canvas.FillRectangle(rect);

                if (captureOwner != Game.CurrentPlayer)
                {
                    canvas.StrokeColor = selectedPiece.Owner == Game.CurrentPlayer ? Colors.Blue : Colors.Red;
                    canvas.StrokeSize = 1.0f;
                    canvas.DrawRectangle(rect);
                }
            }

            // outline the Selected square
            canvas.StrokeColor = Colors.Green;
            canvas.StrokeSize = 1.0f;
            canvas.DrawRectangle(GetRect(SelectedLoc2.Value));
        }
    }
}
