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

        private void ViewBoardButtonClick(object sender, RoutedEventArgs e)
        {
            Opacity = 0.1;
        }

        private void EndGameButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
