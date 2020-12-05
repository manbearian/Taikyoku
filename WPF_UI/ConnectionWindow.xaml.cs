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

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for ConnectionWindow.xaml
    /// </summary>
    public partial class ConnectionWindow : Window
    {
        readonly ShogiClient.ShogiClient client;

        public ConnectionWindow()
        {
            InitializeComponent();

            client = new ShogiClient.ShogiClient();
        }

        private async void connectButton_Click(object sender, RoutedEventArgs e)
        {
            client.connection.On<TaikyokuShogi, Guid>("ReceiveNewGame", (gameObject, gameId) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    var newMessage = $"Recived new game with id {gameId}";
                    messagesList.Items.Add(newMessage);
                });
            });

            client.connection.On<List<GameListElement>>("ReceiveGameList", (gameList) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    gamesList.Items.Clear();
                    gamesList.Items.Add("Games:");//{
                    foreach (var game in gameList)
                    {
                        gamesList.Items.Add(game.Name ?? "<<unknown game>>");
                    }
                });
            });

            try
            {
                await client.connection.StartAsync();
                messagesList.Items.Add("Connection started");
                connectButton.IsEnabled = false;
                sendButton.IsEnabled = true;
                await client.connection.InvokeAsync("GetGames");
            }
            catch (Exception ex)
            {
                messagesList.Items.Add(ex.Message);
            }
        }

        private async void sendButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await client.connection.InvokeAsync("CreateGame", "some game");
            }
            catch (Exception ex)
            {
                messagesList.Items.Add(ex.Message);
            }
        }

        private async void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await client.connection.InvokeAsync("GetGames");
            }
            catch (Exception ex)
            {
                messagesList.Items.Add(ex.Message);
            }
        }
    }
}
