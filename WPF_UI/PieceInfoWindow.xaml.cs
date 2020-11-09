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
using System.Windows.Shapes;

using Oracle;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for PieceInfo.xaml
    /// </summary>
    public partial class PieceInfoWindow : Window
    {
        public PieceInfoWindow()
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

            // figure out our grid size
            int maxMoves = 1;
            for (int i = 0; i < moves.StepRange.Length; ++i)
            {
                maxMoves = Math.Max(maxMoves, moves.StepRange[i] == Movement.Unlimited ? 1 : moves.StepRange[i]);
            }

            for (int i = 0; i < moves.JumpRange.Length; ++i)
            {
                var jumpRange = moves.JumpRange[i].JumpDistances?.Max() + 1;
                jumpRange += moves.JumpRange[i].RangeAfter == Movement.Unlimited ? 1 : moves.JumpRange[i].RangeAfter;
                maxMoves = Math.Max(maxMoves, jumpRange ?? 0);
            }

            if (moves.HookMove.HasValue)
            {
                maxMoves = Math.Max(maxMoves, 2);
            }

            moveGrid.ColumnDefinitions.Clear();
            moveGrid.RowDefinitions.Clear();
            moveGrid.Children.Clear();

            var gridSize = maxMoves * 2 + 1;
            moveGrid.Width = gridSize * 20.0;
            moveGrid.Height = gridSize * 20.0;
            for (int i = 0; i < gridSize; ++i)
            {
                moveGrid.ColumnDefinitions.Add(new ColumnDefinition());
                moveGrid.RowDefinitions.Add(new RowDefinition());
            }

            var glyphGrid = new TextBlock[gridSize, gridSize];

            for (int i = 0; i < gridSize; ++i)
            {
                for (int j = 0; j < gridSize; ++j)
                {
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
                    glyphGrid[i, j] = glyphBox;
                }
            }

            var pieceIcon = glyphGrid[gridSize / 2, gridSize / 2];
            pieceIcon.Text = "☖";
            pieceIcon.FontSize = 14;

            for (int direction = 0; direction < moves.StepRange.Length; ++direction)
            {
                for (int i = 1; i <= Math.Min(moves.StepRange[direction], maxMoves); ++i)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                    glyphGrid[gridX, gridY].Text = GetMoveChar(moves.StepRange[direction], direction);
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
                    glyphGrid[gridX, gridY].Text = "✬";
                }

                var maxJumpRange = (jumpInfo.RangeAfter < Movement.Unlimited) ? jumpInfo.RangeAfter + jumpInfo.JumpDistances.Max() + 2 : Movement.Unlimited;
                for (int i = jumpInfo.JumpDistances.Max() + 2; i <= Math.Min(maxJumpRange, maxMoves); ++i)
                {
                    var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                    glyphGrid[gridX, gridY].Text = GetMoveChar(jumpInfo.RangeAfter, direction);
                }
            }

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


#if false
                            case MoveType.Jump:
                                {
                                    var jumpGlpyh = new FormattedText(
                                        "✬",
                                        CultureInfo.GetCultureInfo("jp-jp"),
                                        FlowDirection.LeftToRight,
                                        new Typeface("MS Gothic"),
                                        SpaceHeight * 0.5,
                                        Brushes.Black,
                                        1.25);
                                    jumpGlpyh.TextAlignment = TextAlignment.Center;
                                    var center = BoardLocToRect(move.Loc).Location;
                                    center.Offset(SpaceWidth / 2, SpaceHeight / 2 - jumpGlpyh.Height / 2);
                                    dc.DrawText(jumpGlpyh, center);
                                    break;
                                }

                            case MoveType.Igui:
                                {
                                    var iguiGlpyh = new FormattedText(
                                        "!",
                                        CultureInfo.GetCultureInfo("jp-jp"),
                                        FlowDirection.LeftToRight,
                                        new Typeface("MS Gothic"),
                                        SpaceHeight * 0.5,
                                        Brushes.Black,
                                        1.25);
                                    iguiGlpyh.TextAlignment = TextAlignment.Center;
                                    var center = BoardLocToRect(move.Loc).Location;
                                    center.Offset(SpaceWidth / 2, SpaceHeight / 2 - iguiGlpyh.Height / 2);
                                    dc.DrawText(iguiGlpyh, center);
                                    break;
                                }
#endif