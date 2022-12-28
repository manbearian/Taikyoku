
using System.Text.Json;
using Microsoft.Maui.Storage;
using ShogiEngine;

namespace MauiUI
{
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

        public static IEnumerable<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGameList
        {
            get
            {
                List<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> localGameList = new();

                string cacheDir = FileSystem.Current.CacheDirectory;

                foreach (var f in Directory.GetFiles(cacheDir).Where(f => Path.GetExtension(f) == ".shogi"))
                {
                    var deserialized = JsonSerializer.Deserialize<TaikyokuShogi>(File.ReadAllBytes(f));
                    var guidOkay = Guid.TryParse(Path.GetFileNameWithoutExtension(f), out var gameId);
                    if (guidOkay && deserialized is not null)
                    {
                        localGameList.Add((gameId, File.GetLastWriteTime(f), deserialized));
                    }
                }

                return localGameList;
            }
        }

        private static string MakeFileName(Guid gameId) =>
            Path.Combine(FileSystem.Current.CacheDirectory, gameId.ToString() + ".shogi");

        public static void SaveGame(TaikyokuShogi game)
             => File.WriteAllBytes(MakeFileName(Guid.NewGuid()), JsonSerializer.SerializeToUtf8Bytes(game));

        public static void DeleteGame(Guid gameId) =>
            File.Delete(MakeFileName(gameId));

        public static void ClearLocalGames()
        {
            string cacheDir = FileSystem.Current.CacheDirectory;
            foreach (var f in Directory.GetFiles(cacheDir).Where(f => Path.GetExtension(f) == ".shogi"))
            {
                File.Delete(f);
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