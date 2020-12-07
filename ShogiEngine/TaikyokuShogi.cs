using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace ShogiEngine
{
    public enum Player
    {
        Black,
        White
    }

    public enum GameEndType
    {
        Checkmate,
        IllegalMove
    }

    [Flags]
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

        private readonly Piece[,] _boardState = new Piece[BoardWidth, BoardHeight];
        private Player? _currentPlayer;

        public Player? CurrentPlayer
        {
            get => _currentPlayer;

            private set
            {
                _currentPlayer = value;
            }
        }
        private Player OtherPlayer { get => CurrentPlayer.Value == Player.Black ? Player.White : Player.Black; }

        public TaikyokuShogi(TaikyokuShogiOptions gameOptions = TaikyokuShogiOptions.None)
        {
            SetInitialBoard();
            CurrentPlayer = Player.Black;
            Options = gameOptions;
        }

        // Constructor for deserialization
        internal TaikyokuShogi(Piece[,] pieces, Player ?currentPlayer, TaikyokuShogiOptions options)
        {
            if (pieces.Rank != 2 || pieces.GetLength(0) != BoardWidth || pieces.GetLength(1) != BoardHeight)
                throw new NotSupportedException();

            _boardState = pieces;
            CurrentPlayer = currentPlayer;
            Options = options;
        }

        public static TaikyokuShogi Deserlialize(ReadOnlySpan<byte> serialBytes) =>
            JsonSerializer.Deserialize<TaikyokuShogi>(serialBytes);

        public byte[] Serialize() =>
            JsonSerializer.SerializeToUtf8Bytes(this, new JsonSerializerOptions());

        public Piece GetPiece((int X, int Y) loc) => _boardState[loc.X, loc.Y];

        public TaikyokuShogiOptions Options { get; }

        public GameEndType? Ending { get; private set; }

        public Player? Winner { get; private set; }

        // Layout the pieces on the board in their starting position
        private void SetInitialBoard()
        {
            _boardState.SetInitialState();
        }

        // Move the turn to the next player
        private void NextTurn() => CurrentPlayer = (CurrentPlayer == Player.Black ? Player.White : Player.Black);

        private void EndGame(GameEndType gameEnding, Player? winner)
        {
            CurrentPlayer = null;
            Ending = gameEnding;
            Winner = winner;
        }

        // Public API: move the piece at startLoc to endLoc
        //   CurrentPlayer is advanced
        //   The optional parameter `midLoc` is used for area-moves (e.g. lion move)
        //   Caller should check `CurrentPlayer` or `Ending` property to determine if the game is over after the move
        public virtual void MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null, bool promote = false)
        {
            var piece = GetPiece(startLoc);

            // nonsencial moves are invalid
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

            // to allow for testing without king/prince on the board, only check for mate on capture of king/prince
            var checkForCheckmate = capturedPieces.Select(loc => _boardState[loc.X, loc.Y]).Any(piece => IsRoyalty(piece.Id));

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
            void IllegalMove() => EndGame(GameEndType.IllegalMove, OtherPlayer);

            static bool IsRoyalty(PieceIdentity id) => id == PieceIdentity.Prince || id == PieceIdentity.King;

            bool IsCheckmate()
            {
                if (!checkForCheckmate)
                    return false;

                foreach (var p in _boardState)
                {
                    if (p?.Owner == OtherPlayer && IsRoyalty(p.Id))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        // Public "debug" API: Set which piece (or no piece) at a board location.
        public void Debug_SetPiece(Piece piece, (int X, int Y) loc)
        {
            if (_boardState[loc.X, loc.Y] == piece)
                return;

            _boardState[loc.X, loc.Y] = piece;
        }

        // Public "debug" API: Set let the next player take a turn
        public void Debug_EndTurn() =>
            NextTurn();


        // Comparison/Equality: value equality for games

        public override bool Equals(object obj) =>
            Equals(obj as TaikyokuShogi);

        public bool Equals(TaikyokuShogi other)
        {
            if (other is null)
                return false;

            if ((_currentPlayer, Options) != (other._currentPlayer, other.Options))
                return false;

            for (int x = 0; x < _boardState.GetLength(0); ++x)
            {
                for (int y = 0; y < _boardState.GetLength(1);  ++y)
                {
                    if (_boardState[x, y] != other._boardState[x, y])
                        return false;
                }
            }

            return true;
        }

        public override int GetHashCode() => (_currentPlayer, Options, _boardState).GetHashCode();

        public static bool operator ==(TaikyokuShogi lhs, TaikyokuShogi rhs) => lhs?.Equals(rhs) ?? rhs is null;

        public static bool operator !=(TaikyokuShogi lhs, TaikyokuShogi rhs) => !(lhs == rhs);
    }
}