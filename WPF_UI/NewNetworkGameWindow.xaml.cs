using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    /// Interaction logic for NewNetworkGameWindow.xaml
    /// </summary>
    public partial class NewNetworkGameWindow : Window
    {
        public ShogiEngine.TaikyokuShogi Game { get; private set; }

        public NewNetworkGameWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var result = Task.Run(() => ShogiClient.Connector.Connect()).Result;

            Game = ShogiEngine.TaikyokuShogi.Deserlialize(result.Span);
            DialogResult = true;
            Close();
        }
    }
}
