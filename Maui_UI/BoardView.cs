using System.Diagnostics.Contracts;

using ShogiEngine;

namespace MauiUI;

internal class BoardDrawer : IDrawable
{
    private BoardView View { get; set; }

    public BoardDrawer(BoardView view) =>
        View = view;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        Contract.Assert(View.Width == dirtyRect.Width);
        Contract.Assert(View.Height == dirtyRect.Height);

        var width = dirtyRect.Width;
        var height = dirtyRect.Height;
        var boardWidth = View.BoardWidth;
        var boardHeight = View.BoardHeight;
        var spaceWidth = View.SpaceWidth;
        var spaceHeight = View.SpaceHeight;

        // we need at least 1 px to draw the grid
        if (width == 0 || height == 0)
            return;

        if (View.Game is not null)
        {
            canvas.Rotate(View.IsRotated ? 180 : 0, width / 2, height / 2);

            // Draw background
            canvas.FillColor = Colors.AntiqueWhite;
            canvas.FillRectangle(0, 0, width, height);

            DrawBoard(canvas, dirtyRect);
            DrawPieces(canvas, dirtyRect);
        }

        void DrawBoard(ICanvas canvas, RectF dirtyRect)
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

        void DrawPieces(ICanvas canvas, RectF dirtyRect)
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
                    var piece = View.Game.GetPiece((i, j));

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

                canvas.StrokeColor = (loc == View.SelectedLoc) ? ((piece.Owner == View.Game.CurrentPlayer) ? Colors.Blue : Colors.Red) : Colors.Black;
                canvas.StrokeSize = 1.0f;
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
    }
}

public class BoardView : GraphicsView
{
    public TaikyokuShogi? Game { get; set; } = new TaikyokuShogi(); // TOOD: set the real game

    public int BoardWidth { get => TaikyokuShogi.BoardWidth; }

    public int BoardHeight { get => TaikyokuShogi.BoardHeight; }

    public float SpaceWidth { get => (float)Width / BoardWidth; }

    public float SpaceHeight { get => (float)Height / BoardHeight; }

    public bool IsRotated { get; set; }

    public (int X, int Y)? SelectedLoc { get; set; } = null;

    public BoardView()
    {
        Drawable = new BoardDrawer(this);
        StartInteraction += BoardView_StartInteraction;
        EndInteraction += BoardView_EndInteraction;
    }

    public (int X, int Y)? GetBoardLoc(Point p)
    {
        // check for negative values before rounding
        if (p.X < 0 || p.Y < 0)
            return null;

        var (x, y) = IsRotated ?
            (BoardWidth - 1 - (int)(p.X / SpaceWidth), BoardHeight - 1 - (int)(p.Y / SpaceHeight))
                : ((int)(p.X / SpaceWidth), (int)(p.Y / SpaceHeight));

        return (x < 0 || x >= BoardWidth || y < 0 || y >= BoardHeight) ? null : (x, y);
    }

    private void BoardView_StartInteraction(object? sender, TouchEventArgs e)
    {
        if (Game is null)
            return;

        var loc = GetBoardLoc(e.Touches.FirstOrDefault());

        if (loc is null)
            return;

        var clickedPiece = Game.GetPiece(loc.Value);

        SelectedLoc = clickedPiece is null ? null : loc;

        Invalidate();
    }

    private void BoardView_EndInteraction(object? sender, TouchEventArgs e)
    {
        if (!e.IsInsideBounds)
            return;

        // TODO: Gestures?

    }
}
