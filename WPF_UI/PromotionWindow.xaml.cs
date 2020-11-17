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

            originalPieceDisplay.SetPiece(new TaiyokuShogi(), PieceIdentity.AncientDragon);
            promotedPieceDisplay.SetPiece(new TaiyokuShogi(), PieceIdentity.GreatDragon);
        }
    }
}
