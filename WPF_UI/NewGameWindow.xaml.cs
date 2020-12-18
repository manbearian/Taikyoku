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
    /// Interaction logic for NewGameWindow.xaml
    /// </summary>
    public partial class NewGameWindow : Window
    {
        public TaikyokuShogi? Game { get; set; }

        public bool NetworkGame { get; private set; }

        public Connection Connection { get; }

        public Guid GameId { get; private set; }

        public Player? LocalPlayer { get; private set; }

        public NewGameWindow()
        {
            InitializeComponent();

            Connection = new Connection();

            Connection.OnReceiveGameStart += RecieveGameStart;
        }

        private void SetUIForConnectExistingGame()
        {
            GameOptionGroupBox.IsEnabled = false;
            LocalRadioButton.IsEnabled = false;
            LocalRadioButton.IsChecked = false;
            NetworkRadioButton.IsChecked = true;
            NewGameButton.Content = "Connect";
            Title = "Add Opponent";
        }

        // Disable all UI elements except the cancel button
        private void SetUIForWaitForConnection()
        {
            GameTypeGroupBox.IsEnabled = false;
            GameOptionGroupBox.IsEnabled = false;
            NewGameButton.IsEnabled = false;
        }

        private void RecieveGameStart(object sender, ReceiveGameStartEventArgs e) =>
            Dispatcher.Invoke(() =>
            {
                Game = e.Game;
                GameId = e.GameId;
                LocalPlayer = e.Player;
                DialogResult = true;
                NetworkGame = true;
                Close();
            });

        private async void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            // todo: pick up game options from UI
            var gameOptions = TaikyokuShogiOptions.None;

            if (LocalRadioButton.IsChecked == true)
            {
                DialogResult = true;
                Game = new TaikyokuShogi(gameOptions);
            }
            else if (NetworkRadioButton.IsChecked == true)
            {
                var localPlayerIsBlack = ColorBox.SelectedIndex == 0;
                var gameName = NameBox.Text;

                try
                {
                    // lock the UI while we wait for a response
                    SetUIForWaitForConnection();

                    await Connection.ConnectAsync().
                        ContinueWith(_ => Connection.RequestNewGame(gameName, gameOptions, localPlayerIsBlack, Game));

                    return;
                }
                catch (Exception)
                {
                    // todo: where do i log this error?
                }
            }
            else
            {
                // unexpected state, treat as cancel
                System.Diagnostics.Debug.Assert(false);
            }

            Close();
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            await Connection.RequestCancelGame(); // cancel any games we started
            Close();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            if (Game != null)
            {
                // if the window creator has set "Game" then we're connecting an existing game rather than creating a new one
                SetUIForConnectExistingGame();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Connection.OnReceiveGameStart -= RecieveGameStart;
        }

    }
}
