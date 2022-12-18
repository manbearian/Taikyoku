using Microsoft.AspNetCore.SignalR;
using ShogiClient;
using ShogiComms;
using ShogiEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        public PlayerColor? LocalPlayer { get; private set; }

        public string? OpponentName { get; private set; }

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

        private void RecieveGameStart(object sender, ReceiveGameStartEventArgs e)
        {
            if (GameId != e.GameInfo.GameId)
            {
                // game start for unknown game; ignore
                // TODO: log this???
                return;
            }

            Dispatcher.Invoke(() =>
            {
                Game = e.Game;
                PlayerId = e.PlayerId;
                DialogResult = true;
                Close();
            });
        }

        private void UpdateGameList(IEnumerable<ClientGameInfo> gameList)
        {
            GamesList.Items.Clear();

            if (IsShowingKnownGames)
            {
                // mark any games unknown to the server as dead
                DeadGames = KnownGames.EmptyIfNull().Where(knownGame => gameList.All(g => g.GameId != knownGame.GameId)).ToList(); // copy list
            }

            if (gameList.Any())
            {
                var orderedList = gameList.OrderByDescending(elem => elem.LastPlayed).ThenByDescending(elem => elem.Created).ThenBy(elem => elem.UnassignedColor());

                foreach (var game in orderedList)
                {
                    GamesList.Items.Add(new ClientGameInfoWrapper(game));
                }

                GamesList.DisplayMemberPath = "DisplayString";
                GamesList.IsEnabled = true;
            }
            else
            {
                GamesList.Items.Add("No Games Available");

                GamesList.DisplayMemberPath = null;
                GamesList.IsEnabled = false;
            }
        }

        private async void JoinGameButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GamesList.SelectedItem;

            if (selectedItem == null)
                return;

            SetUIForWaitForConnection();

            var gameInfo = (ClientGameInfo)(ClientGameInfoWrapper)selectedItem;
            GameId = gameInfo.GameId;

            try
            {
                if (IsShowingKnownGames)
                {
                    var playerId = KnownGames.EmptyIfNull().First(game => game.GameId == gameInfo.GameId).PlayerId;
                    await Connection.RequestRejoinGame(gameInfo.GameId, playerId);
                }
                else
                {
                    Properties.Settings.Default.PlayerName = NameBox.Text;
                    Properties.Settings.Default.Save();

                    LocalPlayer = gameInfo.UnassignedColor();
                    OpponentName = gameInfo.WaitingPlayerName();
                    await Connection.JoinGame(gameInfo.GameId, NameBox.Text);
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
                    await Connection.RequestGameInfo(KnownGames.EmptyIfNull().Select(p => new NetworkGameRequest(p.GameId, p.PlayerId)).ToList()).
                        ContinueWith(t =>
                    {
                        Dispatcher.Invoke(() => UpdateGameList(t.Result));
                    });
                }
                else
                {
                    await Connection.RequestAllOpenGameInfo().ContinueWith(t =>
                    {
                        Dispatcher.Invoke(() => UpdateGameList(t.Result));
                    });
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
                    NameBox.Text = $"{gameInfo.BlackName} vs. {gameInfo.WhiteName}";
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
                get => $"vs {_info.WaitingPlayerName()} ({_info.UnassignedColor().Opponent()})\tLast Played: {_info.LastPlayed.ToLocalTime()}\tCreated: {_info.Created.ToLocalTime()}";
            }

            public static implicit operator ClientGameInfo(ClientGameInfoWrapper wrapper) =>
                wrapper._info;
        }
    }
}
