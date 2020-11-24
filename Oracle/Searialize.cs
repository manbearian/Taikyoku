﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Oracle
{
    internal class TaikyokuJsonConverter : JsonConverter<TaikyokuShogi>
    {
        public override TaikyokuShogi Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            Piece[,] pieces = new Piece[TaikyokuShogi.BoardWidth, TaikyokuShogi.BoardHeight];
            Player? currentPlayer = null;
            TaikyokuShogiOptions gameOptions = TaikyokuShogiOptions.None;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new TaikyokuShogi(pieces, currentPlayer, gameOptions);

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

                    default:
                        throw new JsonException();
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, TaikyokuShogi game, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

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

            writer.WriteEndObject();
        }
    }
}
