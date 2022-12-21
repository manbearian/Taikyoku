using System;
using System.Collections.Generic;

using Microsoft.Azure.Cosmos.Table;
using Microsoft.AspNetCore.SignalR;

using ShogiComms;
using ShogiEngine;


namespace ShogiServerless
{
    internal class PlayerInfo
    {
        public PlayerInfo(string name) : this(name, Guid.NewGuid()) { }

        public PlayerInfo(string name, Guid id) => (PlayerName, PlayerId) = (name, id);

        public Guid PlayerId { get; }

        public string PlayerName { get; }

        public override string ToString() => $"<{PlayerName}-{PlayerId}>";
    }

    internal record class OpenGameInfo
    {
        public TaikyokuShogi Game { get; }

        public Guid GameId { get; }

        public DateTime Created { get; }

        public PlayerInfo WaitingPlayerInfo { get; }

        public PlayerColor WaitingPlayerColor { get; }

        public OpenGameInfo(TaikyokuShogi game, string playerName, PlayerColor playerColor)
        {
            Game = game;
            GameId = Guid.NewGuid();
            Created = DateTime.UtcNow;
            WaitingPlayerInfo = new(playerName);
            WaitingPlayerColor = playerColor;
        }

        // Convert the saved state of this game into information that the client can consume
        public ClientGameInfo ToClientGameInfo() =>
            new()
            {
                GameId = GameId,
                Created = Created,
                LastPlayed = Created,
                BlackName = WaitingPlayerColor == PlayerColor.Black ? WaitingPlayerInfo.PlayerName : null,
                WhiteName = WaitingPlayerColor == PlayerColor.White ? WaitingPlayerInfo.PlayerName : null
            };
    }

    internal class GameInfo : ITableEntity
    {
        // required to allow default construction by Azure Storage
        private TaikyokuShogi? _game;
        private PlayerInfo? _blackPlayer;
        private PlayerInfo? _whitePlayer;

        public TaikyokuShogi Game { get => _game ?? throw new NullReferenceException(); }

        public Guid Id { get; private set; }

        public DateTime Created { get; private set; }

        public DateTime LastPlayed { get; set; }

        public PlayerInfo BlackPlayer { get => _blackPlayer ?? throw new NullReferenceException(); }

        public PlayerInfo WhitePlayer { get => _whitePlayer ?? throw new NullReferenceException(); }

        public GameInfo(OpenGameInfo openGameInfo, string newPlayerName)
        {
            (_game, Id, Created, LastPlayed) = (openGameInfo.Game, openGameInfo.GameId, openGameInfo.Created, openGameInfo.Created);
            if (openGameInfo.WaitingPlayerColor == PlayerColor.Black)
            {
                _blackPlayer = openGameInfo.WaitingPlayerInfo;
                _whitePlayer = new (newPlayerName);
            }
            else
            {
                _whitePlayer = openGameInfo.WaitingPlayerInfo;
                _blackPlayer = new (newPlayerName);
            }

            (((ITableEntity)this).PartitionKey, ((ITableEntity)this).RowKey) = (string.Empty, Id.ToString());
        }

        public PlayerColor GetPlayerColor(Guid playerId) =>
            playerId == BlackPlayer.PlayerId ? PlayerColor.Black : PlayerColor.White;

        public PlayerInfo GetPlayerInfo(Guid playerId) => 
            GetPlayerInfo(GetPlayerColor(playerId));

        public PlayerInfo GetPlayerInfo(PlayerColor player) =>
            player switch
            {
                PlayerColor.Black => BlackPlayer,
                PlayerColor.White => WhitePlayer,
                _ => throw new HubException("unknown player")
            };

        public PlayerInfo GetOtherPlayerInfo(Guid playerId) =>
            GetPlayerInfo(GetPlayerColor(playerId).Opponent());

        // Convert the saved state of this game into information that the client can consume
        public ClientGameInfo ToClientGameInfo() =>
            new ()
            {
                GameId = Id,
                Created = Created,
                LastPlayed = LastPlayed,
                BlackName = BlackPlayer.PlayerName,
                WhiteName = WhitePlayer.PlayerName
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
                ["BlackPlayer_PlayerId"] = new EntityProperty(BlackPlayer.PlayerId),
                ["BlackPlayer_PlayerName"] = new EntityProperty(BlackPlayer.PlayerName),
                ["WhitePlayer_PlayerId"] = new EntityProperty(WhitePlayer.PlayerId),
                ["WhitePlayer_PlayerName"] = new EntityProperty(WhitePlayer.PlayerName)
            };
        }

        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            _game = TaikyokuShogi.Deserlialize(properties["Game"].BinaryValue);
            Id = properties["Id"].GuidValue ?? throw new Exception("Cannot deserialize");
            Created = properties["Created"].DateTime ?? throw new Exception("Cannot deserialize");
            LastPlayed = properties["LastPlayed"].DateTime ?? throw new Exception("Cannot deserialize");

            var blackId = properties["BlackPlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
            if (blackId == Guid.Empty)
                throw new Exception("Cannot deserialize");
            _blackPlayer = new (properties["BlackPlayer_PlayerName"].StringValue ?? throw new Exception("Cannot deserialize"), blackId);
            var whiteId = properties["WhitePlayer_PlayerId"].GuidValue ?? throw new Exception("Cannot deserialize");
            if (whiteId == Guid.Empty)
                throw new Exception("Cannot deserialize");
            _whitePlayer = new (properties["WhitePlayer_PlayerName"].StringValue ?? throw new Exception("Cannot deserialize"), whiteId);
        }

        string? ITableEntity.PartitionKey { get; set; }

        string? ITableEntity.RowKey { get; set; }

        string? ITableEntity.ETag { get; set; }

        DateTimeOffset ITableEntity.Timestamp { get; set; }
    }

}
