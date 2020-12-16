using System;
using System.Text.Json;

using ShogiEngine;

namespace WPF_UI
{
    class GameSaver
    {
        public TaikyokuShogi Game { get; set; }

        public Guid? GameId { get; set; }

        public Player? LocalPlayer { get; set; }

        public static byte[] Save(TaikyokuShogi game, Guid? gameId, Player? LocalPlayer) =>
            JsonSerializer.SerializeToUtf8Bytes(new GameSaver() { Game = game, GameId = gameId, LocalPlayer = LocalPlayer }, new JsonSerializerOptions());

        public static (TaikyokuShogi Game, Guid? GameId, Player? LocalPlayer) Load(ReadOnlySpan<byte> bytes)
        {
            var saveGame = JsonSerializer.Deserialize<GameSaver>(bytes);

            if (saveGame.Game == null)
                throw new JsonException("Corrupted game information in saved game");

            if (saveGame.GameId != null || saveGame.LocalPlayer != null)
            {
                // validate network information before returning the result
                if (saveGame.GameId == null || saveGame.LocalPlayer == null)
                    throw new JsonException("Corrupted network information in saved game");
            }

            return (saveGame.Game, saveGame.GameId, saveGame.LocalPlayer);
        }
    }
}
