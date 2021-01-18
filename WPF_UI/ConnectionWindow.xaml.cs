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

using Microsoft.AspNetCore.SignalR;

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

        public TaikyokuShogi? Game { get; private set; }

        public Guid GameId { get; private set; }

        public Guid PlayerId { get; private set; }

        public Player? LocalPlayer { get; private set; }

        public string? Opponent { get; private set; }

        public IEnumerable<(Guid GameId, Guid PlayerId)>? KnownGames { get; set; }

        public IEnumerable<(Guid GameId, Guid PlayerId)> DeadGames { get; private set; } = Enumerable.Empty<(Guid GameId, Guid PlayerId)>();

        private bool IsShowingKnownGames { get => KnownGames != null; }

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

        private void SetUIForWaitForConnection()
        {
            // Disable all UI elements except the cancel button (TODO: add a cancel button!)
            NameBox.IsEnabled = false;
            JoinGameButton.IsEnabled = false;
            GamesList.IsEnabled = false;
        }

        private void ResetUI()
        {
            // Renable all the UI elements after a canceled connection
            NameBox.IsEnabled = true;
            JoinGameButton.IsEnabled = true;
            GamesList.IsEnabled = true;

            if (IsShowingKnownGames)
                SetUIForConnectExistingGame();
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
                Opponent = e.Opponent;
                DialogResult = true;
                Close();
            });

        private void UpdateGameList(IEnumerable<ClientGameInfo> gameList)
        {
            GamesList.Items.Clear();

            if (IsShowingKnownGames)
            {
                RecordDeadGames(gameList.Select(elem => (elem.GameId, elem.RequestingPlayerId)));
            }

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

            void RecordDeadGames(IEnumerable<(Guid GameId, Guid PlayerId)> serverKnownGames) =>
                DeadGames = KnownGames.Except(serverKnownGames).ToList(); // make a copy of the enumeration
        }

        private async void JoinGameButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GamesList.SelectedItem;

            if (selectedItem == null)
                return;

            SetUIForWaitForConnection();

            var gameInfo = (ClientGameInfo)(ClientGameInfoWrapper)selectedItem;

            try
            {
                if (IsShowingKnownGames)
                {
                    await Connection.RequestRejoinGame(gameInfo.GameId, gameInfo.RequestingPlayerId);
                }
                else
                {
                    Properties.Settings.Default.PlayerName = NameBox.Text;
                    Properties.Settings.Default.Save();

                    await Connection.RequestJoinGame(gameInfo.GameId, NameBox.Text);
                }
            }
            catch (HubException)
            {
                ResetUI();
            }
        }

        private async void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                await Connection.ConnectAsync();

                if (IsShowingKnownGames)
                {
                    SetUIForConnectExistingGame();
                    await Connection.RequestGameInfo(KnownGames.Select(p => new NetworkGameRequest(p.GameId, p.PlayerId)));
                }
                else
                {
                    await Connection.RequestAllOpenGameInfo();
                }
            }
            catch (System.Net.Http.HttpRequestException)
            {
                // bad connection, timeout, etc.
                // TODO: log error? report to uesr?
                Close();
            }
            catch (System.Net.Sockets.SocketException)
            {
                // bad connection, timeout, etc.
                // TODO: log error? report to uesr?
                Close();
            }
            catch (HubException)
            {
                // server couldn't find/load game
                // TODO: log error? report to uesr?
                Close();
            }
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            JoinGameButton.IsEnabled = e.AddedItems.Count > 0;

            if (IsShowingKnownGames)
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
                    NameBox.Text = $"{gameInfo.PlayerName()} ({gameInfo.PlayerColor()})";
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
                get => $"vs {_info.OpponentName()} ({_info.OpponentColor()})\tLast Played: {_info.LastPlayed.ToLocalTime()}\tCreated: {_info.Created.ToLocalTime()}";
            }

            public static implicit operator ClientGameInfo(ClientGameInfoWrapper wrapper) =>
                wrapper._info;
        }
    }
}
