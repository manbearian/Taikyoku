using System;
using System.Diagnostics.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using ShogiEngine;

namespace WPF_UI
{
    static class GameSaver
    {
        private static System.Threading.Mutex gameSaverLock = new System.Threading.Mutex(false, "SHOGI_gameSaverLock");

        // For network games, save enough information to query the server about the game
        //  todo: should we cache other information?
        public static void RecordNetworkGame(Guid gameId, Guid playerId)
        {
            Contract.Assert(gameId != Guid.Empty && playerId != Guid.Empty);

            // this reload/mutux is designed to allow multiple clients to interact with the settings simultaneously
            //   (e.g. white and black players in same game on same machine, but differnet clients)
            if (gameSaverLock.WaitOne(TimeSpan.FromSeconds(1)))
            {
                Properties.Settings.Default.Reload();
                if (Properties.Settings.Default.NetworkGameList == null)
                    Properties.Settings.Default.NetworkGameList = new HashSet<(Guid GameId, Guid PlayerId)>();
                Properties.Settings.Default.NetworkGameList.Add((gameId, playerId));
                Properties.Settings.Default.Save();
                gameSaverLock.ReleaseMutex();
            }
            else
            {
                // we timed out trying to access the save-game file... TODO: what should we do?
                throw new Exception("failed to acquire save game mutex");
            }
        }

        public static void RemoveNetworkGame(Guid gameId, Guid playerId)
        {
            // this reload/mutux is designed to allow multiple clients to interact with the settings simultaneously
            //   (e.g. white and black players in same game on same machine, but differnet clients)
            if (gameSaverLock.WaitOne(TimeSpan.FromSeconds(1)))
            {
                Properties.Settings.Default.Reload();
                Properties.Settings.Default.NetworkGameList.Remove((gameId, playerId));
                Properties.Settings.Default.Save();
                gameSaverLock.ReleaseMutex();

                if (Properties.Settings.Default.LastNetworkGameState == (gameId, playerId))
                {
                    ClearMostRecentGame();
                }
            }
            else
            {
                // we timed out trying to access the save-game file... TODO: what should we do?
                throw new Exception("failed to acquire save game mutex");
            }
        }

        public static IEnumerable<(Guid GameId, Guid PlayerId)> GetNetworkGames()
        {
            // don't need the mutex here, just show whatever is available
            Properties.Settings.Default.Reload();
            return Properties.Settings.Default.NetworkGameList;
        }

        public static void RecordGameState(TaikyokuShogi game, (Guid gameId, Guid playerId)? networkGameInfo = null)
        {
            Properties.Settings.Default.LastGameState = networkGameInfo == null ? game.Serialize() : null;
            Properties.Settings.Default.LastNetworkGameState = networkGameInfo;
            Properties.Settings.Default.Save();
        }

        public static (TaikyokuShogi? GameState, (Guid gameId, Guid playerId)? NetworkGameInfo) LoadMostRecentGame()
        {
            var lastGameState = Properties.Settings.Default.LastGameState;
            var lastNetworkGameState = Properties.Settings.Default.LastNetworkGameState;

            if (lastNetworkGameState != null)
                return (null, lastNetworkGameState);

            if (lastGameState != null)
                return (TaikyokuShogi.Deserlialize(lastGameState), null);

            return (null, null);
        }

        public static void ClearMostRecentGame()
        {
            Properties.Settings.Default.LastNetworkGameState = null;
            Properties.Settings.Default.LastGameState = null;
        }
    }

}
