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
    /// Interaction logic for ConnectionWindow.xaml
    /// </summary>
    public partial class ConnectionWindow : Window
    {
        public Connection Connection { get; }

        public TaikyokuShogi? Game{ get; private set; }

        public Guid GameId { get; private set; }

        public Guid PlayerId { get; private set; }

        public Player? LocalPlayer { get; private set; }

        public ConnectionWindow()
        {
            InitializeComponent();

            var userName = Properties.Settings.Default.PlayerName;
            NameBox.Text = userName == string.Empty ? Environment.UserName : userName;

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
                PlayerId = e.PlayerId;
                LocalPlayer = e.Player;
                DialogResult = true;
                Close();
            });

        private void UpdateGameList(IEnumerable<NetworkGameInfo> gameList)
        {
            GamesList.Items.Clear();

            if (gameList.Count() == 0)
            {
                GamesList.Items.Add("No Games Available");

                GamesList.DisplayMemberPath = null;
                GamesList.IsEnabled = false;
            }
            else
            {
                foreach (var game in gameList)
                {
                    GamesList.Items.Add(new NetworkGameInfoWrapper(game));
                }

                GamesList.DisplayMemberPath = "DisplayString";
                GamesList.IsEnabled = true;
            }
        }

        private async void JoinGameButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GamesList.SelectedItem;

            if (selectedItem == null)
                return;

            Properties.Settings.Default.PlayerName = NameBox.Text;
            await Connection.RequestJoinGame(((NetworkGameInfo)(NetworkGameInfoWrapper)selectedItem).GameId, NameBox.Text);
        }

        private async void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                await Connection.ConnectAsync();
                await Connection.RequestAllOpenGameInfo();
            }
            catch (Exception)
            {
                // todo: where do i log this error?
            }
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            JoinGameButton.IsEnabled = e.AddedItems.Count > 0;

        private void GamesList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
                return;

            GamesList.SelectedIndex = -1;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // disconnect network event handlers on close
            Connection.OnReceiveGameList -= RecieveGameList;
            Connection.OnReceiveGameStart -= RecieveGameStart;
        }
    }

    class NetworkGameInfoWrapper
    {
        private readonly NetworkGameInfo _info;

        public NetworkGameInfoWrapper(NetworkGameInfo info) => _info = info;

        public string DisplayString
        {
            get
            {
                string theirName = _info.BlackName != string.Empty ? _info.BlackName : _info.WhiteName;
                string theirColor = _info.BlackName != string.Empty ? "black" : "white";
                return $"{theirName} ({theirColor})\t{_info.Created}";
            }
        }

        public static implicit operator NetworkGameInfo(NetworkGameInfoWrapper wrapper) =>
            wrapper._info;
    }
}
