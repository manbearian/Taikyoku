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

using ShogiClient;
using ShogiEngine;
using ShogiComms;
using System.Diagnostics;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for ReconnectWindow.xaml
    /// </summary>
    public partial class ReconnectWindow : Window
    {
        public TaikyokuShogi? Game { get; private set; }

        public Connection Connection { get; }

        public string? Opponent { get; private set; }

        public ReconnectWindow(Guid gameId, Guid playerId, PlayerColor myColor)
        {
            InitializeComponent();

            Connection = new Connection(gameId, playerId, myColor);
            Connection.OnReceiveGameStart += RecieveGameStart;
        }

        private void RecieveGameStart(object sender, ReceiveGameStartEventArgs e) =>
            Dispatcher.Invoke(() =>
            {
                Debug.Assert(e.GameInfo.GameId == Connection.GameId);

                Game = e.Game;
                Opponent = Connection.Color == PlayerColor.Black ? e.GameInfo.WhiteName : e.GameInfo.BlackName;
                DialogResult = true;
                Close();
            });

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                await Connection.ConnectAsync();
                await Connection.RejoinGame();
            }
            catch (Exception ex) when (Connection.ExceptionFilter(ex))
            {
                // TODO: log error? report to uesr?
                Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Connection.OnReceiveGameStart -= RecieveGameStart;
        }
    }
}
