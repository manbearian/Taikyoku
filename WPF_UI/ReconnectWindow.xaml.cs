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

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for ReconnectWindow.xaml
    /// </summary>
    public partial class ReconnectWindow : Window
    {
        public TaikyokuShogi? Game { get; private set; }

        public Connection Connection { get; }

        public Guid GameId { get; }

        public Guid PlayerId { get; }

        public Player? LocalPlayer { get; private set; }

        public string? Opponent { get; private set; }

        public ReconnectWindow(Guid gameId, Guid playerId)
        {
            InitializeComponent();

            Connection = new Connection();
            (GameId, PlayerId) = (gameId, playerId);

            Connection.OnReceiveGameStart += RecieveGameStart;
        }

        private void RecieveGameStart(object sender, ReceiveGameStartEventArgs e) =>
            Dispatcher.Invoke(() =>
            {
                // ignore spurious game events
                if (e.GameId != GameId || e.PlayerId != PlayerId)
                    return;

                Game = e.Game;
                LocalPlayer = e.Player;
                Opponent = e.Opponent;
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
                await Connection.RequestRejoinGame(GameId, PlayerId);
            }
            catch (System.Net.Sockets.SocketException)
            {
                // bad connection, timeout, etc.
                Close();
            }
            catch (Microsoft.AspNetCore.SignalR.HubException)
            {
                // server couldn't find/load game
                Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Connection.OnReceiveGameStart -= RecieveGameStart;
        }
    }
}
