using System;

namespace ShogiComms
{
    public class NetworkGameInfo
    {
        public string Name { get; set; }

        public Guid Id { get; set; }
    }

    public class Location
    {
        public int X { get; set; }

        public int Y { get; set; }

        public Location() { }

        public static explicit operator Location((int X, int Y) loc) => new Location() {X = loc.X, Y = loc.Y };

        public static explicit operator Location((int X, int Y)? loc) => loc is null ? null : (Location)loc.Value;

        public static explicit operator (int X, int Y)(Location loc) => (loc.X, loc.Y);

        public static explicit operator (int X, int Y)?(Location loc) => loc is null ? null as (int, int)? : (loc.X, loc.Y);
    }
}
