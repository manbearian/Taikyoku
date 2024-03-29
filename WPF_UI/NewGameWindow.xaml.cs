﻿using System;
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
using System.Diagnostics.Contracts;

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

        public string? OpponentName { get; private set; }

        private bool WaitingForConnection { get => !NewGameButton.IsEnabled; }

        public NewGameWindow()
        {
            InitializeComponent();

            var userName = Properties.Settings.Default.PlayerName;
            NameBox.Text = userName == string.Empty ? Environment.UserName : userName;

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

        private void SetUIForWaitForConnection()
        {
            // Disable all UI elements except the cancel button
            GameTypeGroupBox.IsEnabled = false;
            GameOptionGroupBox.IsEnabled = false;
            NewGameButton.IsEnabled = false;
        }

        private void RecieveGameStart(object sender, ReceiveGameStartEventArgs e)
        {
            Contract.Assert(Connection.GameId == e.GameInfo.GameId);
            Contract.Assert(Connection.PlayerId == e.PlayerId);

            Dispatcher.Invoke(() =>
            {
                NetworkGame = true;
                OpponentName = Connection.Color == PlayerColor.Black ? e.GameInfo.WhiteName : e.GameInfo.BlackName;
                Game = e.Game;
                DialogResult = true;
                Close();
            });
        }

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
                Game ??= new TaikyokuShogi(gameOptions);

                Properties.Settings.Default.PlayerName = NameBox.Text;
                Properties.Settings.Default.Save();

                bool isBlack = ColorBox.SelectedIndex == 0;
                var playerName = NameBox.Text;

                try
                {
                    // lock the UI while we wait for a response
                    SetUIForWaitForConnection();

                    await Connection.ConnectAsync();
                    await Connection.RequestNewGame(playerName, isBlack, Game);
                    return;
                }
                catch (Exception)
                {
                    // todo: where do i log this error?
                    // treat as cancel
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
            if (WaitingForConnection)
            {
                try
                {
                    await Connection.CancelGame(); // cancel any games we started
                }
                catch (Exception)
                {
                    // todo: where do i log this error?
                }
            }

            Close();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            if (Game is not null)
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
