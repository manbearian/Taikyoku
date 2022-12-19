using System;
using System.Diagnostics.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using ShogiEngine;
using WPF_UI.Properties;

namespace WPF_UI
{
    static class GameSaver
    {
        private static readonly System.Threading.Mutex gameSaverLock = new System.Threading.Mutex(false, "SHOGI_gameSaverLock");

        // For network games, save enough information to query the server about the game
        //  todo: should we cache other information?
        public static void RecordNetworkGame(Guid gameId, Guid playerId, PlayerColor myColor)
        {
            Contract.Assert(gameId != Guid.Empty && playerId != Guid.Empty);

            // this reload/mutux is designed to allow multiple clients to interact with the settings simultaneously
            //   (e.g. white and black players in same game on same machine, but differnet clients)
            if (gameSaverLock.WaitOne(TimeSpan.FromSeconds(1)))
            {

                Settings.Default.Reload();

                // TODO: we're still getting duplicates?!!?
                var origList = Settings.Default.NetworkGameList.NetworkGameStates.EmptyIfNull();
                var newList = origList.Append(new NetworkGameState(gameId, playerId, myColor)).Distinct();
                Settings.Default.NetworkGameList = new NetworkGameStateList(newList);
                Settings.Default.Save();
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
                Settings.Default.Reload();

                var updatedList = Settings.Default.NetworkGameList.NetworkGameStates.EmptyIfNull().Where(g => g.GameId != gameId || g.PlayerId != playerId);
                Settings.Default.NetworkGameList = new NetworkGameStateList(updatedList);
                Settings.Default.Save();
                gameSaverLock.ReleaseMutex();

                if (Settings.Default.LastNetworkGameState?.GameId == gameId
                    && Settings.Default.LastNetworkGameState?.PlayerId == playerId)
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

        public static IEnumerable<NetworkGameState> GetNetworkGames()
        {
            // don't need the mutex here, just show whatever is available
            Settings.Default.Reload();
            return Settings.Default.NetworkGameList.NetworkGameStates;
        }

        public static void RecordGameState(TaikyokuShogi game, NetworkGameState? networkGameInfo = null)
        {
            Settings.Default.LastGameState = networkGameInfo is null ? game.Serialize() : null;
            Settings.Default.LastNetworkGameState = networkGameInfo;
            Settings.Default.Save();
        }

        public static (TaikyokuShogi? GameState, NetworkGameState? NetworkGameInfo) LoadMostRecentGame()
        {
            var lastGameState = Settings.Default.LastGameState;
            var lastNetworkGameState = Settings.Default.LastNetworkGameState;

            if (!(lastNetworkGameState is null))
                return (null, lastNetworkGameState);

            if (!(lastGameState is null))
                return (TaikyokuShogi.Deserlialize(lastGameState), null);

            return (null, null);
        }

        public static void ClearMostRecentGame()
        {
            Settings.Default.LastNetworkGameState = null;
            Settings.Default.LastGameState = null;
        }
    }

}
