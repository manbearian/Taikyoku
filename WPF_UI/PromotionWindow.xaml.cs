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
    /// Interaction logic for PromotionWindow.xaml
    /// </summary>
    public partial class PromotionWindow : Window
    {
        public PromotionWindow()
        {
            InitializeComponent();
        }

        public bool ShowDialog(TaiyokuShogi game, PieceIdentity idBefore, PieceIdentity idAfter)
        {
            originalPieceDisplay.SetPiece(game, idBefore);
            promotedPieceDisplay.SetPiece(game, idAfter);
            return ShowDialog() ?? false;
        }

        private void promoteButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void keepButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
