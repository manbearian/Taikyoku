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

using ShogiClient;
using ShogiEngine;
using ShogiComms;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for KnownGamesWindow.xaml
    /// </summary>
    public partial class KnownGamesWindow : Window
    {
        public Connection Connection { get; }

        public TaikyokuShogi? Game { get; private set; }

        public Guid GameId { get; private set; }

        public Guid PlayerId { get; private set; }

        public Player? LocalPlayer { get; private set; }

        private List<MyNetworkGameInfo> KnownGames { get; } = new List<MyNetworkGameInfo>();

        public KnownGamesWindow()
        {
            InitializeComponent();

            Connection = new Connection();

            Connection.OnReceiveGameList += RecieveGameList;
            Connection.OnReceiveGameStart += RecieveGameStart;
        }

        private void RecieveGameList(object sender, ReceiveGameListEventArgs e) =>
            Dispatcher.Invoke(() => UpdateGameList(e.GameList));

        private void RecieveGameStart(object sender, ReceiveGameStartEventArgs e) =>
            Dispatcher.Invoke(() =>
            {
                Game = e.Game;
                GameId = e.GameId;
                PlayerId = e.GameId;
                LocalPlayer = e.Player;
                DialogResult = true;
                Close();
            });

        private void UpdateGameList(IEnumerable<NetworkGameInfo> gameList)
        {
            GameList.Items.Clear();

            if (gameList.Count() == 0)
            {
                GameList.Items.Add("No Games Available");

                GameList.DisplayMemberPath = null;
                GameList.IsEnabled = false;
            }
            else
            {
                foreach (var game in gameList)
                {
                    GameList.Items.Add(game);
                }

                GameList.DisplayMemberPath = "Name";
                GameList.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                await Connection.ConnectAsync();
                // await Connection.RequestGameInfo(GameId, LocalPlayer);
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

        private class MyNetworkGameInfo
        {
            public NetworkGameInfo GameInfo;
            public Player LocalPlayer;

            public MyNetworkGameInfo(NetworkGameInfo x, Player p) => (GameInfo, LocalPlayer) = (x, p);
        }
    }
}
