using System;
using System.Collections.Generic;
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

        public void SetPiece(TaiyokuShogi game, PieceIdentity id)
        {
            var moves = game.GetMovement(id);

            headerText.Text = $"{Pieces.Name(id)}\n{Pieces.Kanji(id)} ({Pieces.Romanji(id)})";

            // figure out our grid size
            int maxMoves = 1;
            for (int i = 0; i < moves.StepRange.Length; ++i)
            {
                maxMoves = Math.Max(maxMoves, moves.StepRange[i] == Movement.FullRange ? 1 : moves.StepRange[i]);
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

            // render moves in a grid
            //     | ○ |  
            //  ---+---+---
            //   ─ | ☖| ─
            //  ---+---+---
            //   ○ |   | ○
            var pieceIcon = new TextBlock();
            pieceIcon.Text = "☖";
            pieceIcon.FontSize = 14;
            pieceIcon.HorizontalAlignment = HorizontalAlignment.Center;
            pieceIcon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(pieceIcon, gridSize / 2);
            Grid.SetRow(pieceIcon, gridSize / 2);
            moveGrid.Children.Add(pieceIcon);

            for (int direction = 0; direction < moves.StepRange.Length; ++direction)
            {
                for (int i = 1; i <= Math.Min(moves.StepRange[direction], maxMoves); ++i)
                {
                    var moveIcon = new TextBlock();
                    moveIcon.Text = moves.StepRange[direction] == 0 ? "" : moves.StepRange[direction] < Movement.FullRange ? "○" : GetFullRangeChar(direction);
                    moveIcon.FontSize = 12;
                    moveIcon.HorizontalAlignment = HorizontalAlignment.Center;
                    moveIcon.VerticalAlignment = VerticalAlignment.Center;
                    var (gridX, gridY) = GetGridPos(gridSize, direction, i);
                    Grid.SetColumn(moveIcon, gridX);
                    Grid.SetRow(moveIcon, gridY);
                    moveGrid.Children.Add(moveIcon);
                }
            }

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
    }
}
