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

using ShogiEngine;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for GameEndWindow.xaml
    /// </summary>
    public partial class GameEndWindow : Window
    {
        public GameEndWindow()
        {
            InitializeComponent();
        }

        public void ShowDialog(GameEndType gameEndType, PlayerColor? winner)
        {
            upperTextBox.Text = gameEndType switch
            {
                GameEndType.Checkmate => "Checkmate!",
                GameEndType.IllegalMove => "Illegal Move!",
                GameEndType.Resignation => $"{winner?.Opponent() ?? throw new NotSupportedException()} has resigned.",
                _ => throw new NotSupportedException()
            };

            lowerTextBox.Text = winner switch
            {
                PlayerColor.White => "White Wins!",
                PlayerColor.Black => "Black Wins!",
                null => "Draw!",
                _ => throw new NotSupportedException(),
            };

            ShowDialog();
        }

        private void OKButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
