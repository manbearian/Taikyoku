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
        public TaikyokuShogi Game { get; private set; }

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

        // tood: pick up game options from UI
        private TaikyokuShogiOptions GameOptions { get => TaikyokuShogiOptions.None; }

        // Disable all UI elements except the cancel button
        private void WaitForConnection()
        {
            GameTypeGroupBox.IsEnabled = false;
            GameOptionGroupBox.IsEnabled = false;
            NewGameButton.IsEnabled = false;
        }

        private async void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                await Connection.ConnectAsync();
            }
            catch (Exception)
            {
                // todo: where do i log this error?
            }
        }

        private void RecieveGameStart(object sender, ReceiveGameUpdateEventArgs e) =>
            Dispatcher.Invoke(() =>
            {
                Game = e.Game;
                GameId = e.GameId;
                DialogResult = true;
                Close();
            });

        private async void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (LocalRadioButton.IsChecked == true)
            {
                DialogResult = true;
                Game = new TaikyokuShogi(GameOptions);
                Close();
            }
            else if (NetworkRadioButton.IsChecked == true)
            {
                NetworkGame = true;
                LocalPlayer = ColorBox.SelectedIndex == 0 ? Player.Black : Player.White;
                await Connection.RequestNewGame(NameBox.Text, GameOptions, LocalPlayer == Player.Black);

                // lock the UI and wait for a response
                WaitForConnection();
            }
            else
            {
                // unexecpted state, treat as cancel
                System.Diagnostics.Debug.Assert(false);
                Close();
            }
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            await Connection.RequestCancelGame(); // cancel any games we started
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Connection.OnReceiveGameStart -= RecieveGameStart;
        }
    }
}
