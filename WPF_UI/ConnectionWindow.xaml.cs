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

using Microsoft.AspNetCore.SignalR.Client;

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

        public TaikyokuShogi Game{ get; private set; }

        public Guid GameId { get; private set; }

        public Player? LocalPlayer { get; private set; }

        private WaitingConnectionWindow _waitWindow;

        public ConnectionWindow()
        {
            InitializeComponent();

            Connection = new Connection();

            Connection.OnReceiveGameList += RecieveGameList;
            Connection.OnReceiveGameStart += RecieveGameStart;
        }

        private void RecieveGameList(object sender, ReceiveGameListEventArgs e) =>
            Dispatcher.Invoke(() => UpdateGameList(e.GameList));

        private void RecieveGameStart(object sender, ReceiveGameUpdateEventArgs e) =>
            Dispatcher.Invoke(() =>
            {
                _waitWindow?.Close();
                Game = e.Game;
                GameId = e.GameId;
                DialogResult = true;
                Close();
            });

        private void UpdateGameList(List<NetworkGameInfo> gameList)
        {
            gamesList.Items.Clear();
            foreach (var game in gameList)
            {
                gamesList.Items.Add(game);
            }
            gamesList.DisplayMemberPath = "Name";
            gamesList.IsEnabled = true;
        }

        private async void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            var nameWindow = new GameNameWindow();
            if (nameWindow.ShowDialog() == true)
            {
                LocalPlayer = Player.Black;
                await Connection.RequestNewGame(nameWindow.GameName, TaikyokuShogiOptions.None, true);

                _waitWindow = new WaitingConnectionWindow();
                if (_waitWindow.ShowDialog() != true)
                {
                    LocalPlayer = null;
                    await Connection.RequestCancelGame();
                }
            }
        }

        private async void JoinGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (gamesList.SelectedIndex < 0)
                return;

            LocalPlayer = Player.White;
            await Connection.RequestJoinGame((gamesList.SelectedItem as NetworkGameInfo).Id);
        }

        private async void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                await Connection.ConnectAsync();
                await Connection.RequestGamesList();
            }
            catch (Exception)
            {
                // todo: where do i log this error?
            }
        }

        private void gamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            JoinGameButton.IsEnabled = e.AddedItems.Count > 0;
        }

        private void gamesList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
                return;
            gamesList.SelectedIndex = -1;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Connection.OnReceiveGameList -= RecieveGameList;
            Connection.OnReceiveGameStart -= RecieveGameStart;
        }
    }
}
