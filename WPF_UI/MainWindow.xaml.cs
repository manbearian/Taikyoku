using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TaiyokuShogi Game = null;

        List<Shape> corners = new List<Shape>();
        List<NumberPanel> borders = new List<NumberPanel>();

        public MainWindow()
        {
            InitializeComponent();

            corners.Add(borderTopLeft);
            corners.Add(borderTopRight);
            corners.Add(borderBottomLeft);
            corners.Add(borderBottomRight);

            borders.Add(borderTop);
            borders.Add(borderBottom);
            borders.Add(borderLeft);
            borders.Add(borderRight);

            NewGame();
        }

        private void NewGame()
        {
            Game = new TaiyokuShogi();
            gameBoard.SetGame(Game);

            Game.OnPlayerChange += OnPlayerChange;

            Game.Reset();
        }

        private void OnPlayerChange(object sender, PlayerChangeEventArgs eventArgs)
        {
            if (eventArgs.NewPlayer == Player.White)
            {
                corners.ForEach(corner => { corner.Fill = Brushes.White; });
                borders.ForEach(border => { border.FillColor = Brushes.White; border.TextColor = Brushes.Black; border.InvalidateVisual(); });
            }
            else if (eventArgs.NewPlayer == Player.Black)
            {
                corners.ForEach(corner => { corner.Fill = Brushes.Black; });
                borders.ForEach(border => { border.FillColor = Brushes.Black; border.TextColor = Brushes.White; border.InvalidateVisual(); });
            }
            else
            {
                throw new NotSupportedException();
            }

            InvalidateVisual();
        }

    }
}
