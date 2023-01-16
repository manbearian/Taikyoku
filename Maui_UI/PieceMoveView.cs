using Microsoft.Maui.Controls.Shapes;
using ShogiEngine;
using System.Runtime.CompilerServices;

namespace MauiUI;

public class PieceMoveView : ContentView
{
    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty PieceIdProperty = BindableProperty.Create(nameof(PieceId), typeof(PieceIdentity), typeof(PieceMoveView));

    public PieceIdentity PieceId
    {
        get => (PieceIdentity)GetValue(PieceIdProperty);
        set => SetValue(PieceIdProperty, value);
    }

    //
    // Internal Properties
    //

    private float CellSize { get => (float)Width / 10; }
    
    private Grid MoveGrid { get; } = new();

    public PieceMoveView()
    {
        Content = MoveGrid;

        MoveGrid.BackgroundColor = Colors.White;
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(PieceId))
        {
            UpdateMoveGrid();
        }

        base.OnPropertyChanged(propertyName);
    }

    // render moves in a grid
    //     | ○ |  
    //  ---+---+---
    //   ─ | ☖| ─
    //  ---+---+---
    //   ○ |   | ○
    private void UpdateMoveGrid()
    {
        var moves = MainPage.Default.Game.GetMovement(PieceId);

        //
        // Compute the grid layout
        //

        MoveGrid.ColumnDefinitions.Clear();
        MoveGrid.RowDefinitions.Clear();
        MoveGrid.Children.Clear();

        int maxMoves = 1;
        for (int i = 0; i < moves.StepRange.Length; ++i)
        {
            maxMoves = Math.Max(maxMoves, moves.StepRange[i] == Movement.Unlimited ? 2 : moves.StepRange[i]);
        }

        for (int i = 0; i < moves.JumpRange.Length; ++i)
        {
            var maxJump = moves.JumpRange[i].JumpDistances?.Max();
            var jumpRange = maxJump == Movement.Unlimited ? 2 : maxJump + 1;
            jumpRange += moves.JumpRange[i].RangeAfter == Movement.Unlimited ? 1 : moves.JumpRange[i].RangeAfter;
            maxMoves = Math.Max(maxMoves, jumpRange ?? 0);
        }

        for (int i = 0; i < moves.RangeCapture.Count; ++i)
        {
            maxMoves = Math.Max(maxMoves, moves.RangeCapture[i] ? 2 : 0);
        }

        maxMoves = Math.Max(maxMoves, moves.LionMove ? 3 : 0);

        maxMoves = Math.Max(maxMoves, moves.HookMove switch
        {
            HookType.Orthogonal => 2,
            HookType.Diagonal => 2,
            HookType.ForwardDiagnal => 3,
            _ => 0
        });

        var gridSize = maxMoves * 2 + 1;
        MoveGrid.WidthRequest = gridSize * CellSize;
        MoveGrid.HeightRequest = gridSize * CellSize;
        for (int i = 0; i < gridSize; ++i)
        {
            MoveGrid.ColumnDefinitions.Add(new());
            MoveGrid.RowDefinitions.Add(new());
        }

        //
        // Draw the elements
        //

        var glyphGrid = new (Label TextBlock, Rectangle Background)[gridSize, gridSize];

        for (int i = 0; i < gridSize; ++i)
        {
            for (int j = 0; j < gridSize; ++j)
            {
                var background = new Rectangle()
                {
                    BackgroundColor = BackgroundColor,
                    WidthRequest = CellSize,
                    HeightRequest = CellSize
                };
                MoveGrid.Add(background, i, j);

                var glyphBox = new Label
                {
                    Text = "",
                    FontSize = CellSize * 0.6,
                    TextColor = Colors.Black,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
                MoveGrid.Add(glyphBox, i, j);

                glyphGrid[i, j] = (glyphBox, background);
            }
        }

        var pieceIcon = glyphGrid[gridSize / 2, gridSize / 2].TextBlock;
        pieceIcon.Text = "☖";
        pieceIcon.FontSize = CellSize * 0.8;

        for (int direction = 0; direction < moves.StepRange.Length; ++direction)
        {
            for (int i = 1; i <= Math.Min(moves.StepRange[direction], maxMoves); ++i)
            {
                var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                glyphGrid[gridX, gridY].TextBlock.Text = GetMoveChar(moves.StepRange[direction], direction);
                glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(moves.StepRange[direction]);
            }
        }

        for (int direction = 0; direction < moves.JumpRange.Length; ++direction)
        {
            var (jumpDistances, rangeAfter) = moves.JumpRange[direction];
            if (jumpDistances is null)
                continue;

            if (jumpDistances.FirstOrDefault() == Movement.Unlimited)
            {
                for (int i = 1; i <= maxMoves; ++i)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                    glyphGrid[gridX, gridY].TextBlock.Text = GetMoveChar(Movement.Unlimited, direction);
                    glyphGrid[gridX, gridY].Background.BackgroundColor = Colors.LightYellow;
                }

                continue;
            }

            foreach (var jumpDistance in jumpDistances)
            {
                var (gridX, gridY) = GetGridPos(gridSize, direction, jumpDistance + 1);
                glyphGrid[gridX, gridY].TextBlock.Text = "✬";
                glyphGrid[gridX, gridY].Background.BackgroundColor = Colors.LightYellow;
            }

            if (rangeAfter > 0)
            {
                var maxJumpRange = (rangeAfter < Movement.Unlimited) ? rangeAfter + jumpDistances.Max() + 2 : Movement.Unlimited;
                for (int i = jumpDistances.Max() + 2; i <= Math.Min(maxJumpRange, maxMoves); ++i)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                    glyphGrid[gridX, gridY].TextBlock.Text = GetMoveChar(rangeAfter, direction);
                    glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(rangeAfter);
                }
            }
        }

        if (moves.LionMove)
        {
            for (int direction = 0; direction < Movement.DirectionCount; ++direction)
            {
                var (gridX, gridY) = GetGridPos(gridSize, direction, 1);
                glyphGrid[gridX, gridY].TextBlock.Text = "!";
                glyphGrid[gridX, gridY].Background.BackgroundColor = Colors.LightGreen;
            }

            for (int direction = 0; direction < Movement.DirectionCountWithJumps; ++direction)
            {
                var (gridX, gridY) = GetGridPos(gridSize, direction, 2);
                glyphGrid[gridX, gridY].TextBlock.Text = "✬";
                glyphGrid[gridX, gridY].Background.BackgroundColor = Colors.LightGreen;
            }
        }
        else if (moves.Igui)
        {
            for (int direction = 0; direction < Movement.DirectionCount; ++direction)
            {
                var (gridX, gridY) = GetGridPos(gridSize, direction, 1);
                glyphGrid[gridX, gridY].TextBlock.Text = "!";
                glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(1);
            }
        }

        for (int direction = 0; direction < moves.RangeCapture.Count; ++direction)
        {
            if (moves.RangeCapture[direction])
            {
                for (int i = 1; i <= maxMoves; ++i)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                    glyphGrid[gridX, gridY].TextBlock.Text = GetMoveChar(Movement.Unlimited, direction);
                    glyphGrid[gridX, gridY].Background.BackgroundColor = Colors.LightSalmon;
                }
            }
        }

        switch (moves.HookMove)
        {
            case HookType.Orthogonal:
                foreach (int direction in Movement.OrthoganalDirectrions)
                {
                    for (int i = 1; i <= 2; ++i)
                    {
                        var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                        glyphGrid[gridX, gridY].TextBlock.Text = "┼";
                        glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(Movement.Unlimited);
                    }
                }

                foreach (int direction in Movement.JumpDirections)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, 2);
                    glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(Movement.Unlimited);
                    glyphGrid[gridX, gridY].TextBlock.Text = direction switch
                    {
                        Movement.UpUpLeft => "─",
                        Movement.UpUpRight => "─",
                        Movement.DownDownLeft => "─",
                        Movement.DownDownRight => "─",
                        Movement.UpLeftLeft => "│",
                        Movement.UpRightRight => "│",
                        Movement.DownLeftLeft => "│",
                        Movement.DownRightRight => "│",
                        _ => throw new InvalidOperationException()
                    };
                }
                break;

            case HookType.Diagonal:
                foreach (int direction in Movement.DiagnalDirectrions)
                {
                    for (int i = 1; i <= 2; ++i)
                    {
                        var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                        glyphGrid[gridX, gridY].TextBlock.Text = "╳";
                        glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(Movement.Unlimited);
                    }
                }
                break;

            case HookType.ForwardDiagnal:
                foreach (int direction in new int[] { Movement.UpLeft, Movement.UpRight })
                {
                    for (int i = 1; i <= 3; ++i)
                    {
                        var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                        glyphGrid[gridX, gridY].TextBlock.Text = "╳";
                        glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(Movement.Unlimited);
                    }
                }

                foreach (int direction in new int[] { Movement.Left, Movement.Right })
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, 2);
                    glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(Movement.Unlimited);
                    glyphGrid[gridX, gridY].TextBlock.Text = direction switch
                    {
                        Movement.Left => "╱",
                        Movement.Right => "╲",
                        _ => throw new InvalidOperationException()
                    };
                }

                foreach (int direction in Movement.JumpDirections)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, 3);
                    glyphGrid[gridX, gridY].Background.BackgroundColor = GetMoveColor(Movement.Unlimited);
                    glyphGrid[gridX, gridY].TextBlock.Text = direction switch
                    {
                        Movement.UpUpLeft => "╱",
                        Movement.UpUpRight => "╲",
                        Movement.UpLeftLeft => "╱",
                        Movement.UpRightRight => "╲",
                        Movement.DownDownLeft => "",
                        Movement.DownDownRight => "",
                        Movement.DownLeftLeft => "╱",
                        Movement.DownRightRight => "╲",
                        _ => throw new InvalidOperationException()
                    };
                }
                break;
        }

        //
        // Draw Grid Lines
        //

        for (int i = 0; i < gridSize - 1; ++i)
        {
            var hl = new Line(0, 0, CellSize * gridSize, 0);
            var vl = new Line(0, 0, 0, CellSize * gridSize);
            SetLineInfo(hl);
            MoveGrid.Add(hl, 0, i);
            MoveGrid.SetColumnSpan(hl, gridSize);
            SetLineInfo(vl);
            MoveGrid.Add(vl, i, 0);
            MoveGrid.SetRowSpan(vl, gridSize);
        }

        //
        // Helper Functions
        //

        static void SetLineInfo(Line l)
        {
            l.Stroke = Colors.Black;
            l.StrokeThickness = 1.0;
            l.StrokeDashArray = new() { 4.0 };
            l.StrokeDashOffset = 1.0;
            l.VerticalOptions = LayoutOptions.End;
            l.HorizontalOptions = LayoutOptions.End;
        }

        static Color GetMoveColor(int amount) => amount < Movement.Unlimited ? Color.FromRgb(0xd0, 0xf0, 0xf0) : Color.FromRgb(0xf0, 0xd0, 0xd0);

        static string GetMoveChar(int amount, int direction) =>
            amount < Movement.Unlimited ? "○" :
            direction switch
            {
                Movement.Up => "│",
                Movement.Down => "│",
                Movement.Left => "─",
                Movement.Right => "─",
                Movement.UpLeft => "╲",
                Movement.UpRight => "╱",
                Movement.DownLeft => "╱",
                Movement.DownRight => "╲",
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

                Movement.UpLeftLeft => (gridSize / 2 - value, gridSize / 2 - (value / 2)),
                Movement.UpRightRight => (gridSize / 2 + value, gridSize / 2 - (value / 2)),
                Movement.DownLeftLeft => (gridSize / 2 - value, gridSize / 2 + (value / 2)),
                Movement.DownRightRight => (gridSize / 2 + value, gridSize / 2 + (value / 2)),
                Movement.UpUpLeft => (gridSize / 2 - (value / 2), gridSize / 2 - value),
                Movement.UpUpRight => (gridSize / 2 + (value / 2), gridSize / 2 - value),
                Movement.DownDownLeft => (gridSize / 2 - (value / 2), gridSize / 2 + value),
                Movement.DownDownRight => (gridSize / 2 + (value / 2), gridSize / 2 + value),
                _ => throw new NotSupportedException()
            };
    }
}