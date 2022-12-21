using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.Cosmos.Table;
using Microsoft.AspNetCore.SignalR;

using ShogiComms;
using ShogiEngine;


namespace ShogiServerless
{
    internal class GameInfo : ITableEntity
    {
        internal class PlayerInfo
        {
            public PlayerInfo(string name) : this(name, Guid.NewGuid()) { }

            public PlayerInfo(string name, Guid id) => (PlayerName, PlayerId) = (name, id);

            public Guid PlayerId { get; }

            public string PlayerName { get; }

            public override string ToString() => $"<{PlayerName}-{PlayerId}>";
        }

        private TaikyokuShogi? _game;

        public TaikyokuShogi Game { get => _game ?? throw new NullReferenceException(); }

        public Guid Id { get; private set; }

        public DateTime Created { get; private set; }

        public DateTime LastPlayed { get; set; }

        public PlayerInfo? BlackPlayer { get; private set; } = null;

        public PlayerInfo? WhitePlayer { get; private set; } = null;

        public bool IsOpen { get => BlackPlayer is null || WhitePlayer is null; }

        public GameInfo(TaikyokuShogi game, string playerName, PlayerColor color)
        {
            (_game, Id, Created, LastPlayed) = (game, Guid.NewGuid(), DateTime.UtcNow, DateTime.Now);
            var playerInfo = new PlayerInfo(playerName);
            if (color == PlayerColor.Black)
                BlackPlayer = playerInfo;
            else
                WhitePlayer = playerInfo;
            (((ITableEntity)this).PartitionKey, ((ITableEntity)this).RowKey) = (string.Empty, Id.ToString());
        }

        public (PlayerInfo oldPlayer, PlayerInfo newPlayer) AddPlayer(string name)
        {
            if (BlackPlayer is null && WhitePlayer is not null)
            {
                BlackPlayer = new PlayerInfo(name);
                return (WhitePlayer, BlackPlayer);
            }

            if (WhitePlayer is null && BlackPlayer is not null)
            {
                WhitePlayer = new PlayerInfo(name);
                return (BlackPlayer, WhitePlayer);
            }

            throw new HubException($"Failed to join game: {Id}");
        }

        public PlayerColor GetPlayerColor(Guid playerId) =>
            playerId == BlackPlayer?.PlayerId ? PlayerColor.Black :
                (playerId == WhitePlayer?.PlayerId ? PlayerColor.White :
                    throw new HubException("unknown player"));

        public PlayerInfo GetPlayerInfo(Guid playerId) => GetPlayerInfo(GetPlayerColor(playerId)) ?? throw new Exception("unkonwn player");

        public PlayerInfo? GetPlayerInfo(PlayerColor player) =>
            player switch
            {
                PlayerColor.Black => BlackPlayer,
                PlayerColor.White => WhitePlayer,
                _ => throw new HubException("unknown player")
            };

        public PlayerInfo? GetOtherPlayerInfo(Guid playerId) =>
            GetPlayerInfo(GetPlayerColor(playerId).Opponent());

        // Convert the saved state of this game into information that the client can consume
        public ClientGameInfo ToClientGameInfo() =>
            new ClientGameInfo()
            {
                GameId = Id,
                Created = Created,
                LastPlayed = LastPlayed,
                BlackName = BlackPlayer?.PlayerName,
                WhiteName = WhitePlayer?.PlayerName
            };

        //
        // implmenting ITableEntity
        //

        // parameterlesss contructor requred for ITableEntry
        public GameInfo() { }

        public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            return new Dictionary<string, EntityProperty>
            {
                ["Game"] = new EntityProperty(Game.Serialize()),
                ["Id"] = new EntityProperty(Id),
                ["Created"] = new EntityProperty(Created),
                ["LastPlayed"] = new EntityProperty(LastPlayed),
                ["BlackPlayer_PlayerId"] = new EntityProperty(BlackPlayer?.PlayerId ?? Guid.Empty),
                ["BlackPlayer_PlayerName"] = new EntityProperty(BlackPlayer?.PlayerName ?? ""),
                ["WhitePlayer_PlayerId"] = new EntityProperty(WhitePlayer?.PlayerId ?? Guid.Empty),
                ["WhitePlayer_PlayerName"] = new EntityProperty(WhitePlayer?.PlayerName ?? "")
            };
        }

        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            _game = TaikyokuShogi.Deserlialize(properties["Game"].BinaryValue);
            Id = properties["Id"].GuidValue ?? throw new Exception("Cannot deserialize");
            Created = properties["Created"].DateTime ?? throw new Exception("Cannot deserialize");
            LastPlayed = properties["LastPlayed"].DateTime ?? throw new Exception("Cannot deserialize");

            var blackId = properties["BlackPlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
            if (blackId != Guid.Empty)
                BlackPlayer = new PlayerInfo(properties["BlackPlayer_PlayerName"].StringValue ?? throw new Exception("Cannot deserialize"), blackId);
            var whiteId = properties["WhitePlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
            if (whiteId != Guid.Empty)
                WhitePlayer = new PlayerInfo(properties["WhitePlayer_PlayerName"].StringValue ?? throw new Exception("Cannot deserialize"), whiteId);
        }

        string? ITableEntity.PartitionKey { get; set; }

        string? ITableEntity.RowKey { get; set; }

        string? ITableEntity.ETag { get; set; }

        DateTimeOffset ITableEntity.Timestamp { get; set; }
    }

}
