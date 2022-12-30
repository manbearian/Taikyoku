
using System.Text.Json;
using Microsoft.Maui.Storage;
using ShogiClient;
using ShogiEngine;

namespace MauiUI
{
    public enum LocalGameUpdate
    {
        Add, Update, Remove
    }

    public class LocalGameUpdateEventArgs : EventArgs
    {
        public Guid GameId { get; }

        public TaikyokuShogi? Game { get; }

        public DateTime LastMove { get; }

        public LocalGameUpdate Update { get; }

        public LocalGameUpdateEventArgs(Guid gameId, TaikyokuShogi? game, DateTime lastMove, LocalGameUpdate update) => (GameId, Game, LastMove, Update) = (gameId, game, lastMove, update);
    }

    public class LocalGameManager
    {
        public delegate void LocalGameUpdateHandler(object sender, LocalGameUpdateEventArgs e);
        public event LocalGameUpdateHandler? OnLocalGameUpdate;

        private static string MakeFileName(Guid gameId) =>
            Path.Combine(FileSystem.Current.CacheDirectory, gameId.ToString() + ".shogi");

        private static TaikyokuShogi? ReadFromFile(string fileName)
            => JsonSerializer.Deserialize<TaikyokuShogi>(File.ReadAllBytes(fileName));

        private static void SaveToFile(string fileName, TaikyokuShogi game)
            => File.WriteAllBytes(fileName, JsonSerializer.SerializeToUtf8Bytes(game));

        public void SaveGame(Guid gameId, TaikyokuShogi game)
        {
            var fileName = MakeFileName(gameId);
            LocalGameUpdate update = LocalGameUpdate.Add;
            if (File.Exists(fileName))
            {
                var oldGame = ReadFromFile(fileName);
                if (oldGame?.BoardStateEquals(game) == true)
                    return;
                update = LocalGameUpdate.Update;
            }
            SaveToFile(fileName, game);
            OnLocalGameUpdate?.Invoke(this, new LocalGameUpdateEventArgs(gameId, game, DateTime.UtcNow, update));
        }

        public void DeleteGame(Guid gameId)
        {
            File.Delete(MakeFileName(gameId));
            OnLocalGameUpdate?.Invoke(this, new LocalGameUpdateEventArgs(gameId, null, DateTime.UtcNow, LocalGameUpdate.Remove));
        }

        public static IEnumerable<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGameList
        {
            get
            {
                List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> localGameList = new();

                string cacheDir = FileSystem.Current.CacheDirectory;

                foreach (var f in Directory.GetFiles(cacheDir).Where(f => Path.GetExtension(f) == ".shogi"))
                {
                    var deserialized = ReadFromFile(f);
                    var guidOkay = Guid.TryParse(Path.GetFileNameWithoutExtension(f), out var gameId);
                    if (guidOkay && deserialized is not null)
                    {
                        localGameList.Add((gameId, File.GetLastWriteTime(f), deserialized));
                    }
                }

                return localGameList;
            }
        }
    }

    internal static class MySettings
    {
        public static string PlayerName
        {
            get => Preferences.Default.Get(nameof(PlayerName), "");
            set => Preferences.Default.Set(nameof(PlayerName), value);
        }

        public static bool RotateBoard
        {
            get => Preferences.Default.Get(nameof(RotateBoard), false);
            set => Preferences.Default.Set(nameof(RotateBoard), value);
        }

        public static LocalGameManager LocalGameManager { get; } = new();

        public static IEnumerable<(Guid GameId, Guid PlayerId, PlayerColor MyColor)> NetworkGameList
        {
            get
            {
                var value = Preferences.Default.Get(nameof(NetworkGameList), string.Empty);
                return ((value != string.Empty) ? JsonSerializer.Deserialize<(Guid GameId, Guid PlayerId, PlayerColor MyColor)[]>(value) : null)
                    ?? Enumerable.Empty<(Guid GameId, Guid PlayerId, PlayerColor MyColor)>();
            }

            set
            {
                Preferences.Default.Set(nameof(NetworkGameList), JsonSerializer.Serialize(value));
            }
        }

        public sealed record class NetworkGameState
        {
            public Guid GameId { get; } = Guid.Empty;
            public Guid PlayerId { get; } = Guid.Empty;
            public PlayerColor MyColor { get; }

            public NetworkGameState() => MyColor = PlayerColor.Black;

            public NetworkGameState(Guid gameId, Guid playerId, PlayerColor myColor) =>
                (GameId, PlayerId, MyColor) = (gameId, playerId, myColor);
        }
    }
}