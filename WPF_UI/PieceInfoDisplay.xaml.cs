using System;
using System.Collections.Generic;
using System.Linq;
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

using Oracle;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for PieceInfoDisplay.xaml
    /// </summary>
    public partial class PieceInfoDisplay : UserControl
    {
        public PieceInfoDisplay()
        {
            InitializeComponent();
        }

        // render moves in a grid
        //     | ○ |  
        //  ---+---+---
        //   ─ | ☖| ─
        //  ---+---+---
        //   ○ |   | ○
        public void SetPiece(TaiyokuShogi game, PieceIdentity id)
        {
            var moves = game.GetMovement(id);

            headerText.Text = $"{Pieces.Name(id)}\n{Pieces.Kanji(id)} ({Pieces.Romanji(id)})";

            //////
            //
            // figure out the grid size
            //
            int maxMoves = 1;
            for (int i = 0; i < moves.StepRange.Length; ++i)
            {
                maxMoves = Math.Max(maxMoves, moves.StepRange[i] == Movement.Unlimited ? 2 : moves.StepRange[i]);
            }

            for (int i = 0; i < moves.JumpRange.Length; ++i)
            {
                var jumpRange = moves.JumpRange[i].JumpDistances?.Max() + 1;
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
            //
            ///////

            var gridSize = maxMoves * 2 + 1;
            moveGrid.ColumnDefinitions.Clear();
            moveGrid.RowDefinitions.Clear();
            moveGrid.Children.Clear();
            moveGrid.Width = gridSize * 20.0;
            moveGrid.Height = gridSize * 20.0;
            for (int i = 0; i < gridSize; ++i)
            {
                moveGrid.ColumnDefinitions.Add(new ColumnDefinition());
                moveGrid.RowDefinitions.Add(new RowDefinition());
            }

            var glyphGrid = new (TextBlock TextBlock, Rectangle Background)[gridSize, gridSize];

            for (int i = 0; i < gridSize; ++i)
            {
                for (int j = 0; j < gridSize; ++j)
                {
                    var background = new Rectangle();
                    Grid.SetColumn(background, i);
                    Grid.SetRow(background, j);
                    moveGrid.Children.Add(background);

                    var glyphBox = new TextBlock
                    {
                        Text = "",
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(glyphBox, i);
                    Grid.SetRow(glyphBox, j);
                    moveGrid.Children.Add(glyphBox);

                    glyphGrid[i, j] = (glyphBox, background);
                }
            }

            var pieceIcon = glyphGrid[gridSize / 2, gridSize / 2].TextBlock;
            pieceIcon.Text = "☖";
            pieceIcon.FontSize = 14;

            for (int direction = 0; direction < moves.StepRange.Length; ++direction)
            {
                for (int i = 1; i <= Math.Min(moves.StepRange[direction], maxMoves); ++i)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                    glyphGrid[gridX, gridY].TextBlock.Text = GetMoveChar(moves.StepRange[direction], direction);
                    glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(moves.StepRange[direction]);
                }
            }

            for (int direction = 0; direction < moves.JumpRange.Length; ++direction)
            {
                var jumpInfo = moves.JumpRange[direction];
                if (jumpInfo.JumpDistances == null)
                    continue;

                foreach (var jumpDistance in jumpInfo.JumpDistances)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, jumpDistance + 1);
                    glyphGrid[gridX, gridY].TextBlock.Text = "✬";
                    glyphGrid[gridX, gridY].Background.Fill = Brushes.LightYellow;
                }

                var maxJumpRange = (jumpInfo.RangeAfter < Movement.Unlimited) ? jumpInfo.RangeAfter + jumpInfo.JumpDistances.Max() + 2 : Movement.Unlimited;
                for (int i = jumpInfo.JumpDistances.Max() + 2; i <= Math.Min(maxJumpRange, maxMoves); ++i)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                    glyphGrid[gridX, gridY].TextBlock.Text = GetMoveChar(jumpInfo.RangeAfter, direction);
                    glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(jumpInfo.RangeAfter);
                }
            }

            if (moves.LionMove)
            {
                for (int direction = 0; direction < Movement.DirectionCount; ++direction)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, 1);
                    glyphGrid[gridX, gridY].TextBlock.Text = "!";
                    glyphGrid[gridX, gridY].Background.Fill = Brushes.LightGreen;
                }

                for (int direction = 0; direction < Movement.DirectionCountWithJumps; ++direction)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, 2);
                    glyphGrid[gridX, gridY].TextBlock.Text = "✬";
                    glyphGrid[gridX, gridY].Background.Fill = Brushes.LightGreen;
                }
            }
            else if (moves.Igui)
            {
                for (int direction = 0; direction < Movement.DirectionCount; ++direction)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, 1);
                    glyphGrid[gridX, gridY].TextBlock.Text = "!";
                    glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(1);
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
                        glyphGrid[gridX, gridY].Background.Fill = Brushes.LightSalmon;
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
                            glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(Movement.Unlimited);
                        }
                    }

                    foreach (int direction in Movement.JumpDirections)
                    {
                        var (gridX, gridY) = GetGridPos(gridSize, direction, 2);
                        glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(Movement.Unlimited);
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
                            glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(Movement.Unlimited);
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
                            glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(Movement.Unlimited);
                        }
                    }

                    foreach (int direction in new int[] { Movement.Left, Movement.Right })
                    {
                        var (gridX, gridY) = GetGridPos(gridSize, direction, 2);
                        glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(Movement.Unlimited);
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
                        glyphGrid[gridX, gridY].Background.Fill = GetMoveColor(Movement.Unlimited);
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

            static Brush GetMoveColor(int amount) => new SolidColorBrush(amount < Movement.Unlimited ? Color.FromRgb(0xd0, 0xf0, 0xf0) : Color.FromRgb(0xf0, 0xd0, 0xd0));

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
}

