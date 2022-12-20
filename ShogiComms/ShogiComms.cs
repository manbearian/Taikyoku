using System;
using System.Collections.Generic;
using System.Linq;

namespace ShogiComms
{
    public enum GameStatus
    {
        BlackTurn, WhiteTurn, Expired
    }

    public class NetworkGameRequest
    {
        public Guid GameId { get; set; }

        public Guid RequestingPlayerId { get; set; }

        public NetworkGameRequest() { }

        public NetworkGameRequest(Guid gameId, Guid requestingPlayerId) =>
            (GameId, RequestingPlayerId) = (gameId, requestingPlayerId);
    }

    // Workaround for issue passing lists to Hub not serializing properly
    public class NetworkGameRequestList
    {
        public NetworkGameRequestList(IEnumerable<NetworkGameRequest> list) =>
            List = list.ToArray();

        public NetworkGameRequest[] List { get; } = Array.Empty<NetworkGameRequest>();
    }

    public static class NetworkGameRequestListExtensions
    {
        public static NetworkGameRequestList ToNetworkGameRequestList(this IEnumerable<NetworkGameRequest> list) =>
            new NetworkGameRequestList(list);
    }

    // Workaround for returning named tuples from Hub which was causing silent failure
    public record class GamePlayerPair
    {
        public Guid GameId { get; set; } = Guid.Empty;

        public Guid PlayerId { get; set; } = Guid.Empty;

        public void Deconstruct(out Guid gameId, out Guid playerId) { gameId = GameId; playerId = PlayerId; }

        public GamePlayerPair(Guid gameId, Guid playerId) => (GameId, PlayerId) = (gameId, playerId);
    }

    public record class ClientGameInfo
    {
        public Guid GameId { get; set; }

        public string? BlackName { get; set; } = null;

        public string? WhiteName { get; set; } = null;

        public DateTime Created { get; set; }

        public DateTime LastPlayed { get; set; }

        public GameStatus Status { get; set; }

        public bool IsOpen() => BlackName is null || WhiteName is null;
    }

    public class Location
    {
        public int X { get; set; }

        public int Y { get; set; }

        public Location() { }

        // i'm using (-1,-1) when marshalling because null is invalid
        private bool isValid() => (X >= 0 && Y >= 0);

        public static Location Invalid { get; } = new Location() { X = -1, Y = -1 };

        public static explicit operator Location((int X, int Y) loc) => new() { X = loc.X, Y = loc.Y };

        public static explicit operator Location((int X, int Y)? loc) => loc is null ? Invalid : (Location)loc.Value;

        public static explicit operator (int X, int Y)(Location loc) => (loc.X, loc.Y);

        public static explicit operator (int X, int Y)?(Location loc) => !loc.isValid() ? null : (loc.X, loc.Y);
    }
}
