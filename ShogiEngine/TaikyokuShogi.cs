using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace ShogiEngine
{
    public enum PlayerColor
    {
        Black,
        White
    }

    public static class PlayerExtension
    {
        public static PlayerColor Opponent(this PlayerColor p) => p switch
            {
                PlayerColor.Black => PlayerColor.White,
                PlayerColor.White => PlayerColor.Black,
                _ => throw new NotSupportedException()
            };
    }

    public enum GameEndType
    {
        Checkmate,
        IllegalMove,
        Resignation
    }

    [Flags]

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TaikyokuShogiOptions
    {
        None =                            0,
        ViolentBearAlternative =          1,
        TreacherousFoxAlternative =       2,
        DivineSparrowAlternative =        4,
        EarthDragonAlternative =          8,
        FreeDemonAlternative =         0x10,
        LongNosedGoblinAlternative =   0x20,
        CapricornAlternative =         0x40,
    }

    [JsonConverter(typeof(TaikyokuJsonConverter))]
    public class TaikyokuShogi
    {
        public const int BoardHeight = 36;
        public const int BoardWidth = 36;

        private readonly Piece?[,] _boardState = new Piece?[BoardWidth, BoardHeight];
        private PlayerColor? _currentPlayer;

        public PlayerColor? CurrentPlayer
        {
            get => _currentPlayer;

            private set
            {
                _currentPlayer = value;
            }
        }

        public TaikyokuShogi(TaikyokuShogiOptions gameOptions = TaikyokuShogiOptions.None)
        {
            SetInitialBoard();
            CurrentPlayer = PlayerColor.Black;
            Options = gameOptions;
        }

        // Constructor for deserialization
        internal TaikyokuShogi(TaikyokuShogiOptions options, Piece?[,] pieces, PlayerColor? currentPlayer, GameEndType? ending, PlayerColor? winner, MoveRecorder moves)
        {
            if (pieces.Rank != 2 || pieces.GetLength(0) != BoardWidth || pieces.GetLength(1) != BoardHeight)
                throw new NotSupportedException();

            // validate that GameEndType/Winner are a valid combination
            switch (ending)
            {
                case GameEndType.Checkmate:
                case GameEndType.IllegalMove:
                case GameEndType.Resignation:
                    if (winner == null)
                        throw new ArgumentException("Invalid Game Ending", nameof(ending));
                    break;
                case null:
                    if (winner != null)
                        throw new ArgumentException("Invalid Game Ending", nameof(winner));
                    break;
                default:
                    throw new ArgumentException("Invalid Game Ending", nameof(ending));
            }

            _boardState = pieces;
            _moveRecorder = moves;
            Ending = ending;
            Winner = winner;
            CurrentPlayer = currentPlayer;
            Options = options;
        }

        public static TaikyokuShogi Deserlialize(ReadOnlySpan<byte> serialBytes) =>
            JsonSerializer.Deserialize<TaikyokuShogi>(serialBytes) ?? throw new NullReferenceException();

        public byte[] Serialize() =>
            JsonSerializer.SerializeToUtf8Bytes(this, new JsonSerializerOptions());

        public Piece? GetPiece((int X, int Y) loc) => _boardState[loc.X, loc.Y];

        public Piece GetKnownPiece((int X, int Y) loc) => _boardState[loc.X, loc.Y] ?? throw new NullReferenceException();

        public TaikyokuShogiOptions Options { get; }

        public GameEndType? Ending { get; private set; }

        public PlayerColor? Winner { get; private set; }

        public int MoveCount { get => _moveRecorder.Count; }

        public IEnumerable<MoveRecorder.MoveDescription> Moves { get => _moveRecorder.Moves; }

        private readonly MoveRecorder _moveRecorder = new MoveRecorder();

        // Layout the pieces on the board in their starting position
        private void SetInitialBoard()
        {
            _boardState.SetInitialState();
        }

        // Move the turn to the next player
        private void NextTurn() => CurrentPlayer = (CurrentPlayer == PlayerColor.Black ? PlayerColor.White : PlayerColor.Black);

        private void EndGame(GameEndType gameEnding, PlayerColor? winner)
        {
            CurrentPlayer = null;
            Ending = gameEnding;
            Winner = winner;
        }

        public void Resign(PlayerColor resigningPlayer)
        {
            EndGame(GameEndType.Resignation, resigningPlayer.Opponent());
        }

        // Public API: move the piece at startLoc to endLoc
        //   CurrentPlayer is advanced
        //   The optional parameter `midLoc` is used for area-moves (e.g. lion move)
        //   Caller should check `CurrentPlayer` and/or `Ending` property to determine if the game is over after the move
        public void MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null, bool promote = false)
        {
            var piece = GetPiece(startLoc);

            // nonsensicial moves are invalid
            if (CurrentPlayer == null || piece == null)
                throw new InvalidOperationException("invalid move requested");

            // moving your oppontents piece is illegal
            if (piece.Owner != CurrentPlayer)
            {
                IllegalMove();
                return;
            }

            var moves = this.GetLegalMoves(piece, startLoc, midLoc).Where(move => move.Loc == endLoc);

            if (!moves.Any())
            {
                IllegalMove();
                return;
            }

            var capturedPieces = new List<(int X, int Y)>();

            if (midLoc != null)
            {
                if (!moves.Any(move => move.Type == MoveType.Area || move.Type == MoveType.Igui))
                {
                    IllegalMove();
                    return;
                }

                // capture any piece that got run over by the area-move
                if (_boardState[midLoc.Value.X, midLoc.Value.Y] != null)
                {
                    capturedPieces.Add(midLoc.Value);
                }
            }

            if (moves.Any(move => move.Type == MoveType.RangedCapture))
            {
                int xCount = startLoc.X - endLoc.X;
                int yCount = startLoc.Y - endLoc.Y;

                // orthoganal or diagonal only
                if (xCount != 0 && yCount != 0 && Math.Abs(xCount) != Math.Abs(yCount))
                {
                    IllegalMove();
                    return;
                }

                var xMultiplier = xCount == 0 ? 0 : (xCount > 0 ? -1 : 1);
                var yMultiplier = yCount == 0 ? 0 : (yCount > 0 ? -1 : 1);
                for (int i = 0; i < Math.Max(Math.Abs(xCount), Math.Abs(yCount)); ++i)
                {
                    int x = startLoc.X + xMultiplier * i;
                    int y = startLoc.Y + yMultiplier * i;
                    if (_boardState[x, y] != null)
                    {
                        capturedPieces.Add((x, y));
                    }
                }
            }

            if (_boardState[endLoc.X, endLoc.Y] != null)
            {
                capturedPieces.Add(endLoc);
            }

            // validate promotion
            if (promote && Movement.CheckPromotion(piece, startLoc, endLoc, capturedPieces.Any()) == PromotionType.None)
            {
                IllegalMove();
                return;
            }

            // record the move
            _moveRecorder.PushMove(startLoc, endLoc, midLoc, promote ? piece.Id : null as PieceIdentity?, capturedPieces.Select(elem => (GetKnownPiece(elem), elem)));

            // to allow for testing without king/prince on the board, only check for mate on capture of king/prince
            var checkForCheckmate = capturedPieces.Select(loc => GetKnownPiece(loc)).Any(piece => IsRoyalty(piece.Id));

            // remove captured pieces
            foreach (var (x, y) in capturedPieces)
            {
                _boardState[x, y] = null;
            }

            // set new location
            _boardState[startLoc.X, startLoc.Y] = null;
            _boardState[endLoc.X, endLoc.Y] = promote ? piece.Promote() : piece;

            if (IsCheckmate())
            {
                Checkmate();
                return;
            }

            NextTurn();
            return;

            void Checkmate() => EndGame(GameEndType.Checkmate, CurrentPlayer);
            void IllegalMove() => EndGame(GameEndType.IllegalMove, CurrentPlayer?.Opponent());

            static bool IsRoyalty(PieceIdentity id) => id == PieceIdentity.Prince || id == PieceIdentity.King;

            bool IsCheckmate()
            {
                if (!checkForCheckmate)
                    return false;

                foreach (var p in _boardState)
                {
                    if (p != null && p.Owner == CurrentPlayer?.Opponent() && IsRoyalty(p.Id))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void UndoLastMove()
        {
            var moveRecord = _moveRecorder.PopMove();

            var piece = GetKnownPiece(moveRecord.EndLoc);

            // unpromote
            if (moveRecord.PromotedFrom != null)
                piece = new Piece(piece.Owner, moveRecord.PromotedFrom.Value, false);

            // move back to start
            _boardState[moveRecord.StartLoc.X, moveRecord.StartLoc.Y] = piece;

            // replace captured pieces
            foreach (var capture in moveRecord.Captures)
            {
                _boardState[capture.Location.X, capture.Location.Y] = capture.Piece;
            }
        }

        // Public "debug" API: Set which piece (or no piece) at a board location.
        public void Debug_SetPiece(Piece? piece, (int X, int Y) loc)
        {
            if (_boardState[loc.X, loc.Y] == piece)
                return;

            _boardState[loc.X, loc.Y] = piece;
        }

        // Public "debug" API: Set let the next player take a turn
        public void Debug_EndTurn() =>
            NextTurn();

        public bool BoardStateEquals(TaikyokuShogi other)
        {
            if (_currentPlayer != other._currentPlayer)
                return false;

            for (int x = 0; x < _boardState.GetLength(0); ++x)
            {
                for (int y = 0; y < _boardState.GetLength(1); ++y)
                {
                    if (_boardState[x, y] != other._boardState[x, y])
                        return false;
                }
            }

            return true;
        }
    }
}