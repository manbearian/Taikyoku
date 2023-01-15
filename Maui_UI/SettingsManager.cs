
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;
using ShogiClient;
using ShogiEngine;

namespace MauiUI;

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
public class NetworkGameManager
{
    static readonly JsonSerializerOptions serializationOptions = new() { IncludeFields = true };

    public void SaveGame(Guid gameId, Guid playerId, PlayerColor myColor)
    {
        var origList = NetworkGameList;
        var newList = origList.Append((gameId, playerId, myColor)).Distinct();
        NetworkGameList = newList;
    }

    public void DeleteGame(Guid gameId, Guid playerId)
    {
        var origList = NetworkGameList;
        var updatedList = origList.Where(g => g.GameId != gameId || g.PlayerId != playerId);
        NetworkGameList = updatedList;
    }

    public static IEnumerable<(Guid GameId, Guid PlayerId, PlayerColor MyColor)> NetworkGameList
    {
        get
        {
            var value = Preferences.Default.Get(nameof(NetworkGameList), string.Empty);
            return ((value != string.Empty) ? JsonSerializer.Deserialize<IEnumerable<(Guid GameId, Guid PlayerId, PlayerColor MyColor)>>(value, serializationOptions) : null)
                ?? Enumerable.Empty<(Guid GameId, Guid PlayerId, PlayerColor MyColor)>();
        }

        private set => Preferences.Default.Set(nameof(NetworkGameList), JsonSerializer.Serialize(value, serializationOptions));
    }
}

internal static class SettingsManager
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

    public static NetworkGameManager NetworkGameManager { get; } = new();
}