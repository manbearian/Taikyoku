using System.Text.Json;

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

public interface ILocalGameSaver
{
    delegate void LocalGameUpdateHandler(object sender, LocalGameUpdateEventArgs e);
    event LocalGameUpdateHandler? OnLocalGameUpdate;

    void SaveGame(Guid gameId, TaikyokuShogi game);
    void DeleteGame(Guid gameId);
    IEnumerable<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGameList { get; }
}

public class LocalGameSaver : ILocalGameSaver
{
    public static LocalGameSaver Default { get; } = new();

    public event ILocalGameSaver.LocalGameUpdateHandler? OnLocalGameUpdate;

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

    public IEnumerable<(Guid GameId, DateTime lastPlayed, TaikyokuShogi Game)> LocalGameList
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
