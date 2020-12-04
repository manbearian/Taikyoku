using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace ShogiEngine
{
    internal class TaikyokuJsonConverter : JsonConverter<TaikyokuShogi>
    {
        private int ComputeChecksum(TaikyokuShogi game)
        {
            int checksum = 0;
            for (int x = 0; x < TaikyokuShogi.BoardWidth; ++x)
            {
                for (int y = 0; y < TaikyokuShogi.BoardWidth; ++y)
                {
                    var piece = game.GetPiece((x, y));
                    checksum += piece != null ? (x + 1) * (y + 1) * ((int)piece.Id + 1) * ((int)piece.Owner + 1) * (piece.Promoted ? 1 : 2) : 0;
                }
            }
            return checksum;
        }

        public override TaikyokuShogi Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            Piece[,] pieces = new Piece[TaikyokuShogi.BoardWidth, TaikyokuShogi.BoardHeight];
            Player? currentPlayer = null;
            TaikyokuShogiOptions gameOptions = TaikyokuShogiOptions.None;
            int checksum = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    var game = new TaikyokuShogi(pieces, currentPlayer, gameOptions);

                    var computedChecksum = ComputeChecksum(game);

                    if (computedChecksum != checksum)
                        throw new JsonException("Unable to validate checksum");

                    return game;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException();

                switch (reader.GetString())
                {
                    case "_boardState":
                        reader.Read();
                        if (reader.TokenType != JsonTokenType.StartArray)
                            throw new JsonException();

                        for (int x = 0; x < TaikyokuShogi.BoardWidth; ++x)
                        {
                            for (int y = 0; y < TaikyokuShogi.BoardWidth; ++y)
                            {
                                reader.Read();
                                if (reader.TokenType == JsonTokenType.Null)
                                    continue;

                                if (reader.TokenType != JsonTokenType.StartArray)
                                    throw new JsonException();

                                reader.Read();
                                var owner = Enum.Parse<Player>(reader.GetString());
                                reader.Read();
                                var pieceId = Enum.Parse<PieceIdentity>(reader.GetString());
                                reader.Read();
                                bool promoted = false;
                                if (reader.TokenType == JsonTokenType.String && reader.GetString() == "promoted")
                                {
                                    promoted = true;
                                    reader.Read();
                                }
                                if (reader.TokenType != JsonTokenType.EndArray)
                                    throw new JsonException();

                                pieces[x, y] = new Piece(owner, pieceId, promoted);
                            }
                        }

                        reader.Read();
                        if (reader.TokenType != JsonTokenType.EndArray)
                            throw new JsonException();
                        break;

                    case "CurrentPlayer":
                        reader.Read();
                        if (reader.TokenType != JsonTokenType.String)
                            throw new JsonException();
                        if (Enum.TryParse<Player>(reader.GetString(), out var player))
                            currentPlayer = player;
                        break;

                    case "Options":
                        reader.Read();
                        if (reader.TokenType != JsonTokenType.StartArray)
                            throw new JsonException();
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.EndArray)
                                break;
                            if (Enum.TryParse<TaikyokuShogiOptions>(reader.GetString(), out var option))
                                gameOptions |= option;
                        }
                        break;

                    case "Checksum":
                        reader.Read();
                        if (reader.TokenType != JsonTokenType.Number)
                            throw new JsonException();
                        checksum = reader.GetInt32();
                        break;

                    default:
                        throw new JsonException();
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, TaikyokuShogi game, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            if (game.CurrentPlayer == null)
            {
                writer.WriteNull("CurrentPlayer");
            }
            else
            {
                writer.WriteString("CurrentPlayer", game.CurrentPlayer.ToString());
            }

            writer.WriteStartArray("Options");
            for (int i = 0; i < sizeof(TaikyokuShogiOptions) * 8; ++i)
            {
                var flag = (TaikyokuShogiOptions)(1 << i);
                if ((game.Options & flag) == flag)
                {
                    writer.WriteStringValue(flag.ToString());
                }
            }
            writer.WriteEndArray();

            writer.WriteStartArray("_boardState");
            for (int x = 0; x < TaikyokuShogi.BoardWidth; ++x)
            {
                for (int y = 0; y < TaikyokuShogi.BoardWidth; ++y)
                {
                    var piece = game.GetPiece((x, y));
                    if (piece == null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        writer.WriteStartArray();
                        writer.WriteStringValue(piece.Owner.ToString());
                        writer.WriteStringValue(piece.Id.ToString());
                        if (piece.Promoted)
                            writer.WriteStringValue("promoted");
                        writer.WriteEndArray();
                    }
                }
            }
            writer.WriteEndArray();

            writer.WriteNumber("Checksum", ComputeChecksum(game));

            writer.WriteEndObject();
        }
    }
}
