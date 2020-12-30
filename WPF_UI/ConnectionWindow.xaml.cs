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

        public IEnumerable<(Guid GameId, Guid PlayerId)>? KnownGames { get; set; }

        public ConnectionWindow()
        {
            InitializeComponent();

            var userName = Properties.Settings.Default.PlayerName;
            NameBox.Text = userName == string.Empty ? Environment.UserName : userName;

            Connection = new Connection();
            Connection.OnReceiveGameList += RecieveGameList;
            Connection.OnReceiveGameStart += RecieveGameStart;
        }

        private void SetUIForConnectExistingGame()
        {
            NameBox.IsEnabled = false;
            NameBox.Text = string.Empty;
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

        private void UpdateGameList(IEnumerable<ClientGameInfo> gameList)
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
                var orderedList = gameList.OrderByDescending(elem => elem.LastPlayed).ThenByDescending(elem => elem.Created).ThenBy(elem => elem.ClientColor);

                foreach (var game in orderedList)
                {
                    GamesList.Items.Add(new ClientGameInfoWrapper(game));
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
            Properties.Settings.Default.Save();

            var gameInfo = ((ClientGameInfo)(ClientGameInfoWrapper)selectedItem);

            if (KnownGames == null)
            {
                await Connection.RequestJoinGame(gameInfo.GameId, NameBox.Text);
            }
            else
            {
                await Connection.RequestRejoinGame(gameInfo.GameId, gameInfo.RequestingPlayerId);
            }
        }

        private async void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                await Connection.ConnectAsync();

                if (KnownGames == null)
                {
                    // Window opened in mode where user wants to search for a game
                    await Connection.RequestAllOpenGameInfo();
                }
                else
                {
                    // Window opened in a mode where user will select a game they've played before
                    SetUIForConnectExistingGame();
                    await Connection.RequestGameInfo(KnownGames.Select(p => new NetworkGameRequest(p.GameId, p.PlayerId)));
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                // bad connection, timeout, etc.
                // TODO: log error? report to uesr?
                Close();
            }
            catch (Microsoft.AspNetCore.SignalR.HubException)
            {
                // server couldn't find/load game
                // TODO: log error? report to uesr?
                Close();
            }
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            JoinGameButton.IsEnabled = e.AddedItems.Count > 0;

            if (KnownGames != null)
            {
                // mode were UI is selecting an existing game
                // update 'our' name to be the name that we registered when we joined the game
                if (e.AddedItems.Count == 0)
                {
                    NameBox.Text = string.Empty;
                }
                else
                {
                    var gameInfo = (ClientGameInfo)(ClientGameInfoWrapper)GamesList.SelectedItem;
                    NameBox.Text = gameInfo.PlayerName();
                }
            }
        }

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

        class ClientGameInfoWrapper
        {
            private readonly ClientGameInfo _info;

            public ClientGameInfoWrapper(ClientGameInfo info) => _info = info;

            public string DisplayString
            {
                get => $"vs {_info.OpponentName()} ({_info.OpponentColor()})\t{_info.Created}";
            }

            public static implicit operator ClientGameInfo(ClientGameInfoWrapper wrapper) =>
                wrapper._info;
        }
    }
}
