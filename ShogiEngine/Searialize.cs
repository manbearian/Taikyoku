using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShogiEngine
{
    public static class SearializatinExtensions
    {
        public static TaikyokuShogi ToTaikyokuShogi(this string serializedGame) =>
            JsonSerializer.Deserialize<TaikyokuShogi>(serializedGame) ?? throw new JsonException("failed to deserialize");

        public static string ToJsonString(this TaikyokuShogi game) =>
            JsonSerializer.Serialize(game) ?? throw new JsonException("failed to serialize");
    }

    internal sealed class TaikyokuStringConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string);

        public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value)
        {
            if (value is string valueAsString)
            {
                return JsonSerializer.Deserialize<TaikyokuShogi>(valueAsString);
            }
            return base.ConvertFrom(context, culture, value ?? throw new NullReferenceException());
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                return JsonSerializer.Serialize(value);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }


    public class TaikyokuJsonConverter : JsonConverter<TaikyokuShogi>
    {
        public override TaikyokuShogi Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            Piece?[,] pieces = new Piece?[TaikyokuShogi.BoardWidth, TaikyokuShogi.BoardHeight];
            MoveRecorder moveRecorder = new MoveRecorder();
            PlayerColor? currentPlayer = null;
            PlayerColor? winner = null;
            GameEndType? ending = null;
            TaikyokuShogiOptions gameOptions = TaikyokuShogiOptions.None;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    try
                    {
                        return new TaikyokuShogi(gameOptions, pieces, currentPlayer, ending, winner, moveRecorder);
                    }
                    catch (ArgumentException e)
                    {
                        throw new JsonException("Failed to construct the game object", e);
                    }
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException();

                switch (reader.GetString())
                {
                    case "_boardState":
                        reader.Read();
                        if (reader.TokenType != JsonTokenType.StartArray)
                            throw new JsonException();

                        for (int y = 0; y < TaikyokuShogi.BoardWidth; ++y)
                        {
                            for (int x = 0; x < TaikyokuShogi.BoardWidth; ++x)
                            {
                                reader.Read();
                                pieces[x, y] = reader.GetPiece();
                            }
                        }

                        reader.Read();
                        if (reader.TokenType != JsonTokenType.EndArray)
                            throw new JsonException();
                        break;

                    case "CurrentPlayer":
                        reader.Read();
                        currentPlayer = reader.GetEnum<PlayerColor>();
                        break;

                    case "Ending":
                        reader.Read();
                        ending = reader.GetEnum<GameEndType>();
                        break;

                    case "Winner":
                        reader.Read();
                        winner = reader.GetEnum<PlayerColor>();
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

                    case "Moves":
                        reader.Read();
                        foreach (var move in reader.GetMoves())
                            moveRecorder.PushMove(move);
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

            writer.WriteString("CurrentPlayer", game.CurrentPlayer?.ToString());

            writer.WriteString("Ending", game.Ending?.ToString());

            writer.WriteString("Winner", game.Winner?.ToString());

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
            for (int y = 0; y < TaikyokuShogi.BoardWidth; ++y)
            {
                for (int x = 0; x < TaikyokuShogi.BoardWidth; ++x)
                {
                    var piece = game.GetPiece((x, y));
                    writer.WriteValue(piece);
                }
            }
            writer.WriteEndArray();

            writer.Write("Moves", game.Moves);

            writer.WriteEndObject();
        }
    }

    static class JsonReadHelpers
    {
        public static (int X, int Y)? GetLocation(this ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException();

            reader.Read();
            var x = reader.GetInt32();
            reader.Read();
            var y = reader.GetInt32();

            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException();

            return (x, y);
        }

        public static Piece? GetPiece(this ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException();

            reader.Read();
            var owner = Enum.Parse<PlayerColor>(reader.GetString() ?? throw new JsonException());

            reader.Read();
            var pieceId = reader.GetPieceIdentity() ?? throw new JsonException();

            reader.Read();
            var promoted = false;
            if (reader.TokenType == JsonTokenType.String)
            {
                if (reader.GetString() != "promoted")
                    throw new JsonException();
                promoted = true;
                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException();

            return new Piece(owner, pieceId, promoted);
        }

        public static PieceIdentity? GetPieceIdentity(this ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException();

            return Enum.Parse<PieceIdentity>(reader.GetString() ?? throw new JsonException());
        }
        public static T? GetEnum<T>(this ref Utf8JsonReader reader) where T : struct
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.String => reader.GetString()?.ToEnum<T>(),
                _ => throw new JsonException()
            };
        }

        // { { }, { }, { } }
        public static IEnumerable<MoveRecorder.MoveDescription> GetMoves(this ref Utf8JsonReader reader)
        {
            var moves = new List<MoveRecorder.MoveDescription>();

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return moves;

                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException();

                reader.Read();
                var startLoc = reader.GetLocation() ?? throw new JsonException();
                reader.Read();
                var endLoc = reader.GetLocation() ?? throw new JsonException();
                reader.Read();
                var midLoc = reader.GetLocation();
                reader.Read();
                var promotedFrom = reader.GetPieceIdentity();

                var captures = new List<(Piece piece, (int X, int Y) Location)>();

                reader.Read();
                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;

                    if (reader.TokenType != JsonTokenType.StartArray)
                        throw new JsonException();

                    reader.Read();
                    var piece = reader.GetPiece() ?? throw new JsonException();
                    reader.Read();
                    var loc = reader.GetLocation() ?? throw new JsonException();

                    captures.Add((piece, loc));

                    reader.Read();
                    if (reader.TokenType != JsonTokenType.EndArray)
                        throw new JsonException();
                }

                moves.Add(new MoveRecorder.MoveDescription(startLoc, endLoc, midLoc, promotedFrom, captures));

                reader.Read();
                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException();
            }

            throw new JsonException();
        }
    }

    static class JsonWriteHelpers
    {
        public static void Write(this Utf8JsonWriter writer, string name, Piece? piece) =>
            Write_Internal(writer, name, piece);

        public static void Write(this Utf8JsonWriter writer, string name, (int X, int Y)? loc) =>
            Write_Internal(writer, name, loc);

        public static void Write(this Utf8JsonWriter writer, string name, IEnumerable<MoveRecorder.MoveDescription> moves) =>
            Write_Internal(writer, name, moves);

        public static void WriteValue(this Utf8JsonWriter writer, Piece? piece) =>
            Write_Internal(writer, null, piece);

        public static void WriteValue(this Utf8JsonWriter writer, (int X, int Y)? loc) =>
            Write_Internal(writer, null, loc);

        public static void WriteValue(this Utf8JsonWriter writer, PieceIdentity? piece) =>
            Write_Internal(writer, null, piece);

        public static void WriteValue(this Utf8JsonWriter writer, IEnumerable<MoveRecorder.MoveDescription> moves) =>
            Write_Internal(writer, null, moves);

        public static void Write_Internal(this Utf8JsonWriter writer, string? name, PieceIdentity? piece)
        {
            if (piece is null)
            {
                writer.WriteNull_Internal(name);
                return;
            }

            if (name is null)
                writer.WriteStringValue(piece.Value.ToString());
            else
                writer.WriteString(name, piece.Value.ToString());
        }

        private static void Write_Internal(this Utf8JsonWriter writer, string? name, Piece? piece)
        {
            if (piece is null)
            {
                writer.WriteNull_Internal(name);
                return;
            }

            if (name is null)
                writer.WriteStartArray();
            else
                writer.WriteStartArray(name);

            writer.WriteStringValue(piece.Owner.ToString());
            writer.WriteValue(piece.Id);
            if (piece.Promoted)
                writer.WriteStringValue("promoted");

            writer.WriteEndArray();
        }

        private static void Write_Internal(this Utf8JsonWriter writer, string? name, (int X, int Y)? loc)
        {
            if (loc is null)
            {
                writer.WriteNull_Internal(name);
                return;
            }

            if (name is null)
                writer.WriteStartArray();
            else
                writer.WriteStartArray(name);

            writer.WriteNumberValue(loc.Value.X);
            writer.WriteNumberValue(loc.Value.Y);

            writer.WriteEndArray();
        }

        public static void Write_Internal(this Utf8JsonWriter writer, string? name, IEnumerable<MoveRecorder.MoveDescription> moves)
        {
            if (name is null)
                writer.WriteStartArray();
            else
                writer.WriteStartArray(name);

            foreach (var move in moves)
            {
                writer.WriteStartArray();

                writer.WriteValue(move.StartLoc);
                writer.WriteValue(move.EndLoc);
                writer.WriteValue(move.MidLoc);
                writer.WriteValue(move.PromotedFrom);

                writer.WriteStartArray();

                foreach (var capture in move.Captures)
                {
                    writer.WriteStartArray();

                    writer.WriteValue(capture.Piece);
                    writer.WriteValue(capture.Location);

                    writer.WriteEndArray();
                }

                writer.WriteEndArray();

                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        private static void WriteNull_Internal(this Utf8JsonWriter writer, string? name)
        {
            if (name is null)
                writer.WriteNullValue();
            else
                writer.WriteNull(name);
        }
    }

    static class EnumExtension
    {
        public static T? ToEnum<T>(this string s) where T : struct
        {
            if (!Enum.TryParse<T>(s, out var e))
                return null;
            return e;
        }
    }
}
