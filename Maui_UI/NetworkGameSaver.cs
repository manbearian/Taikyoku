using System.Text.Json;

using ShogiEngine;

namespace MauiUI;

public interface INetworkGameSaver
{
    void SaveGame(Guid gameId, Guid playerId, PlayerColor myColor);

    void DeleteGame(Guid gameId, Guid playerId);

    IEnumerable<(Guid GameId, Guid PlayerId, PlayerColor MyColor)> NetworkGameList { get; }
}

public class NetworkGameSaver : INetworkGameSaver
{
    public static NetworkGameSaver Default { get; } = new();

    static readonly JsonSerializerOptions serializationOptions = new() { IncludeFields = true };

    public void SaveGame(Guid gameId, Guid playerId, PlayerColor myColor)  =>
        NetworkGameList = NetworkGameList.Append((gameId, playerId, myColor)).Distinct();

    public void DeleteGame(Guid gameId, Guid playerId) =>
        NetworkGameList = NetworkGameList.Where(g => g.GameId != gameId || g.PlayerId != playerId);

    public IEnumerable<(Guid GameId, Guid PlayerId, PlayerColor MyColor)> NetworkGameList
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
