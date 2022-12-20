using Microsoft.AspNetCore.SignalR;
using ShogiClient;
using ShogiComms;
using ShogiEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPF_UI.Properties;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for ConnectionWindow.xaml
    /// </summary>
    public partial class ConnectionWindow : Window
    {
        public Connection Connection { get; }

        public TaikyokuShogi? Game { get; private set; }
        public string? OpponentName { get; private set; }

        public IEnumerable<NetworkGameState>? KnownGames { get; set; }

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
            Contract.Assert(Connection.GameId == e.GameInfo.GameId);

            Dispatcher.Invoke(() =>
            {
                Game = e.Game;
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
                DeadGames = KnownGames.
                    EmptyIfNull().
                    Where(knownGame => gameList.All(g => g.GameId != knownGame.GameId)).
                    Select(elem => (elem.GameId, elem.PlayerId)).
                    ToList(); // copy list
            }

            if (gameList.Any())
            {
                var orderedList = gameList.OrderByDescending(elem => elem.LastPlayed).ThenByDescending(elem => elem.Created);

                foreach (var game in orderedList)
                {
                    if (IsShowingKnownGames)
                    {
                        var localMapping = KnownGames.EmptyIfNull().Where(g => g.GameId == game.GameId);
                        Contract.Assert(localMapping.Count() == 1 || localMapping.Count() == 2);
                        foreach (var localInfo in localMapping)
                        {
                            GamesList.Items.Add(new ClientGameInfoWrapper(game, localInfo));
                        }
                    }
                    else
                    {
                        GamesList.Items.Add(new ClientGameInfoWrapper(game));
                    }
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

            var gameInfo = ((ClientGameInfoWrapper)selectedItem).GameInfo;
            var localInfo = ((ClientGameInfoWrapper)selectedItem).LocalInfo;

            try
            {
                if (IsShowingKnownGames)
                {
                    Contract.Assert(!(localInfo is null));
                    OpponentName = localInfo.MyColor == PlayerColor.Black ? gameInfo.WhiteName : gameInfo.BlackName;
                    Connection.SetGameInfo(gameInfo.GameId, localInfo.PlayerId, localInfo.MyColor);
                    await Connection.RequestRejoinGame();
                }
                else
                {
                    Settings.Default.PlayerName = NameBox.Text;
                    Settings.Default.Save();

                    OpponentName = gameInfo.WaitingPlayerName();
                    Connection.SetGameInfo(gameInfo.GameId, Guid.Empty, gameInfo.UnassignedColor());
                    await Connection.JoinGame(NameBox.Text);
                }
            }
            catch (Exception ex) when (Connection.ExceptionFilter(ex))
            {
                // TODO: log error? report to uesr?
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
            catch (Exception ex) when (Connection.ExceptionFilter(ex))
            {
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
                    var gameInfo = ((ClientGameInfoWrapper)GamesList.SelectedItem).GameInfo;
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
            public ClientGameInfo GameInfo { get; }
            public NetworkGameState? LocalInfo { get; }

            public ClientGameInfoWrapper(ClientGameInfo gameInfo, NetworkGameState? localInfo = null) => (GameInfo, LocalInfo) = (gameInfo, localInfo);

            public string DisplayString
            {
                get
                {
                    string gameName;
                    if (GameInfo.IsOpen())
                        gameName = $"vs. {GameInfo.WaitingPlayerName()} ({GameInfo.UnassignedColor().Opponent()})";
                    else if (LocalInfo?.MyColor == PlayerColor.Black)
                        gameName = $"vs. {GameInfo.WhiteName} (white)";
                    else
                        gameName = $"vs. {GameInfo.BlackName} (black)";
                    return $"{gameName}\tLast Played: {GameInfo.LastPlayed.ToLocalTime()}\tCreated: {GameInfo.Created.ToLocalTime()}";
                } 
            }
        }
    }
}
