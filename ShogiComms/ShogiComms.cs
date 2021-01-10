using System;

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

    public class ClientGameInfo
    {
        public Guid GameId { get; set; }

        public string ClientColor { get; set; } = string.Empty;

        public Guid RequestingPlayerId { get; set; }

        public string BlackName { get; set; } = string.Empty;

        public string WhiteName { get; set; } = string.Empty;

        public DateTime Created { get; set; }

        public DateTime LastPlayed { get; set; }

        public GameStatus Status { get; set; }
    }

    public class Location
    {
        public int X { get; set; }

        public int Y { get; set; }

        public Location() { }

        public static explicit operator Location((int X, int Y) loc) => new Location() {X = loc.X, Y = loc.Y };

        public static explicit operator Location?((int X, int Y)? loc) => loc is null ? null : (Location)loc.Value;

        public static explicit operator (int X, int Y)(Location loc) => (loc.X, loc.Y);

        public static explicit operator (int X, int Y)?(Location loc) => loc is null ? null as (int, int)? : (loc.X, loc.Y);
    }
}
