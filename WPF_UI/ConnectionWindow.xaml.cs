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
        private readonly ShogiClient.ShogiClient _shogiClient;

        private WaitingConnectionWindow _waitWindow;

        public ConnectionWindow()
        {
            InitializeComponent();

            _shogiClient = new ShogiClient.ShogiClient();

            _shogiClient.OnReceiveGameList += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    gamesList.Items.Clear();
                    foreach (var game in e.GameList)
                    {
                        gamesList.Items.Add(game.Name ?? "<<unknown game>>");
                    }
                });
            };

            _shogiClient.OnReceiveGameStart += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _waitWindow?.Close();
                    _waitWindow = null;
                });
            };
        }

        private async void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            var nameWindow = new GameNameWindow();
            
            if (nameWindow.ShowDialog() == true)
            {
                await _shogiClient.RequestNewGame(nameWindow.GameName, TaikyokuShogiOptions.None, true);

                _waitWindow = new WaitingConnectionWindow();
                _waitWindow.ShowDialog();
            }
        }

        private async void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                await _shogiClient.ConnectAsync();
                await _shogiClient.RequestGamesList();
            }
            catch (Exception _)
            {
                // todo: where do i log this error?
            }
        }

        private void gamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            JoinGameButton.IsEnabled = e.AddedItems.Count > 0;
        }
    }
}
