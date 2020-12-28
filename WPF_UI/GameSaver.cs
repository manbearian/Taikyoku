using System;
using System.Diagnostics.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using ShogiEngine;

namespace WPF_UI
{
    class GameSaver
    {
        public TaikyokuShogi? Game { get; set; }

        public Guid GameId { get; set; }

        public Guid PlayerId { get; set; }

        public static void Save(TaikyokuShogi game, Guid gameId, Guid playerId)
        {
            Contract.Requires((gameId == Guid.Empty && playerId == Guid.Empty) || (gameId != Guid.Empty && playerId != Guid.Empty));

            if (Properties.Settings.Default.GameList == null)
                Properties.Settings.Default.GameList = new Hashtable();

            Properties.Settings.Default.GameList[gameId] =
                JsonSerializer.SerializeToUtf8Bytes(new GameSaver() { Game = game, GameId = gameId, PlayerId = playerId }, new JsonSerializerOptions());
            Properties.Settings.Default.LastGame = gameId;
            Properties.Settings.Default.Save();
        }

        public static Dictionary<Guid, byte[]> AllGames()
            => Properties.Settings.Default.GameList.Cast<(Guid, byte[])>().ToDictionary(p => p.Item1, p => p.Item2);

        public static (TaikyokuShogi Game, Guid GameId, Guid PlayerId) LoadMostRecent() =>
            Load(Properties.Settings.Default.LastGame);

        public static (TaikyokuShogi Game, Guid GameId, Guid PlayerId) Load(Guid gameId)
        {
            var gameList = Properties.Settings.Default.GameList;
            if (gameList == null)
                throw new JsonException("Unable to load save game information");

            var gameState = Properties.Settings.Default.GameList[gameId] as byte[];
            var saveGame = JsonSerializer.Deserialize<GameSaver>(gameState);

            if (saveGame.Game == null)
                throw new JsonException("Corrupted game information in saved game");

            // validate network information before returning the result
            if ((saveGame.GameId != Guid.Empty || saveGame.PlayerId != Guid.Empty)
                && (saveGame.GameId == Guid.Empty || saveGame.PlayerId == Guid.Empty))
            {
                throw new JsonException("Corrupted network information in saved game");
            }

            return (saveGame.Game, saveGame.GameId, saveGame.PlayerId);
        }
    }
}
