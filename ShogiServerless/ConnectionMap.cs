using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShogiServerless
{
    internal class ConnectionMap
    {
        private Dictionary<string, (Guid GameId, Guid PlayerId)> _connectionToPlayer { get; } = new Dictionary<string, (Guid GameId, Guid PlayerId)>();
        private Dictionary<(Guid GameId, Guid PlayerId), string> _playerToConnection { get; } = new Dictionary<(Guid GameId, Guid PlayerId), string>();

        private readonly object _lock = new();

        public bool TryGetConnection(Guid gameId, Guid playerId, out string connectionId)
        {
            lock (_lock)
            {
                var success = _playerToConnection.TryGetValue((gameId, playerId), out var value);
                connectionId = success ? value ?? string.Empty : string.Empty;
                return success;
            }
        }

        public bool TryGetPlayer(string connectionId, out Guid gameId, out Guid playerId)
        {
            lock (_lock)
            {
                var success = _connectionToPlayer.TryGetValue(connectionId, out var pair);
                (gameId, playerId) = success ? pair : (Guid.Empty, Guid.Empty);
                return success;
            }
        }

        // remove connectionId from mappings
        // Returns previous connection mapping to (gameId, playerId) if it exists
        public (Guid GameId, Guid PlayerId)? UnmapConnection(string connectionId)
        {
            lock (_lock)
            {
                if (_connectionToPlayer.TryGetValue(connectionId, out var oldGamePlayerPair))
                {
                    _playerToConnection.Remove(oldGamePlayerPair);
                    _connectionToPlayer.Remove(connectionId);
                }
                return oldGamePlayerPair == (Guid.Empty, Guid.Empty) ? null : oldGamePlayerPair;
            }
        }

        // Creates the mapping: connectionId <==> (game, player)
        // Returns previous mamppings for both connectionId and (game, player)
        public (string? OldConnection, (Guid GameId, Guid PlayerId)? OldGamePlayerPair) MapConnection(string connectionId, Guid gameId, Guid playerId)
        {
            lock (_lock)
            {
                // remove stale values
                //
                // INITIAL         map(A, g, p)             map(B, x, y)
                //  A => (x, y)      A => (g, p)              A => (x, y)  <-- stale
                //  (x,y) => A       (x,y) => A  <-- stale    (x, y) => B
                //                   (g,p) => A               B => (x, y)
                //
                if (_connectionToPlayer.TryGetValue(connectionId, out var oldGamePlayerPair))
                {
                    _playerToConnection.Remove(oldGamePlayerPair);
                }

                if (_playerToConnection.TryGetValue((gameId, playerId), out var oldConnection))
                {
                    _connectionToPlayer.Remove(oldConnection);
                }

                // map new values: Connection <=> (Game, Player)
                _connectionToPlayer[connectionId] = (gameId, playerId);
                _playerToConnection[(gameId, playerId)] = connectionId;
                return (oldConnection, oldGamePlayerPair == (Guid.Empty, Guid.Empty) ? null : oldGamePlayerPair);
            }
        }
    }
}
