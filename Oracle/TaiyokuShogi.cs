﻿using System;
using System.Collections.Generic;
using System.Linq;


namespace Oracle
{
    public enum Player
    {
        Black,
        White
    }

    [Flags]
    public enum TaiyokuShogiOptions
    {
        ViolentBearAlternative,
        TreacherousFoxAlternative,
        DivineSparrowAlternative,
        EarthDragonAlternative,
        FreeDemonAlternative,
        LongNosedGoblinAlternative,
        CapricornAlternative,
    }

    public class TaiyokuShogi
    {
        public const int BoardHeight = 36;
        public const int BoardWidth = 36;

        private readonly Piece[,] _boardState = new Piece[BoardWidth, BoardHeight];
        private Player _currentPlayer;

        public Player CurrentPlayer
        {
            get => _currentPlayer;

            private set
            {
                var prevPlayer = _currentPlayer;
                _currentPlayer = value;

                if (OnPlayerChange != null)
                {
                    PlayerChangeEventArgs args = new PlayerChangeEventArgs(prevPlayer, _currentPlayer);
                    OnPlayerChange(this, args);
                }
            }
        }

        public TaiyokuShogi()
        {
        }

        public delegate void PlayerChangeHandler(object sender, PlayerChangeEventArgs e);
        public event PlayerChangeHandler OnPlayerChange;

        public delegate void BoardChangeHandler(object sender, BoardChangeEventArgs e);
        public event BoardChangeHandler OnBoardChange;

        public Piece GetPiece((int X, int Y) loc) => _boardState[loc.X, loc.Y];

        public TaiyokuShogiOptions Options { get; }

        // Layout the pieces on the board in their starting position
        private void SetInitialBoard()
        {
            _boardState.SetInitialState();

            if (OnBoardChange != null)
            {
                var args = new BoardChangeEventArgs();
                OnBoardChange(this, args);
            }
        }

        // Move the turn to the next player
        private void NextTurn() => CurrentPlayer = (CurrentPlayer == Player.Black ? Player.White : Player.Black);

        // Public API: move the piece at startLoc to endLoc
        //   Raises "OnBoardChange" event if move completes (i.e. was legal)
        //   CurrentPlayer is advanced
        //   The optional parameter `midLoc` is used for area-moves (e..g lion move)
        //   return value indicates if the move is legal and was thus completed
        public bool MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null, bool promote = false)
        {
            var piece = GetPiece(startLoc);

            if (piece == null || piece.Owner != CurrentPlayer)
                return false;

            var moves = this.GetLegalMoves(piece, startLoc, midLoc).Where(move => move.Loc == endLoc);

            if (!moves.Any())
                return false;

            var capturedPieces = new List<(int X, int Y)>();

            if (midLoc != null)
            {
                if (!moves.Any(move => move.Type == MoveType.Area || move.Type == MoveType.Igui))
                    return false;

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
                    return false;

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
                return false;

            // Check for game ending move
            CheckGameEndingMove(capturedPieces);

            // remove captured pieces
            foreach (var (x, y) in capturedPieces)
            {
                _boardState[x, y] = null;
            }

            // set new location
            _boardState[startLoc.X, startLoc.Y] = null;
            _boardState[endLoc.X, endLoc.Y] = promote ? piece.Promote() : piece;

            if (OnBoardChange != null)
            {
                var args = new BoardChangeEventArgs();
                OnBoardChange(this, args);
            }

            NextTurn();
            return true;
        }

        // Public API: Reset the game to its initial state
        public void Reset()
        {
            SetInitialBoard();
            CurrentPlayer = Player.Black;
        }

        private bool CheckGameEndingMove(IReadOnlyList<(int X, int Y)> capturedPieces)
        {
            static bool IsRoyalty(PieceIdentity id) => id == PieceIdentity.Prince || id == PieceIdentity.King;

            // trigger this search only on a capture of king/prince
            if (!capturedPieces.Select(loc => _boardState[loc.X, loc.Y]).Any(piece => IsRoyalty(piece.Id)))
                return false;

            // No way to capture your own king/prince (ranged-capture lets one capture their own non-royal pieces)
            if (capturedPieces.Select(loc => _boardState[loc.X, loc.Y]).Where(piece => IsRoyalty(piece.Id)).Any(piece => piece.Owner == CurrentPlayer))
            {
                // cannot capture ones own royalty
                throw new NotSupportedException();
            }

            var remainingRoyals = new List<(int X, int Y)>();

            // find all the royal pieces
            for (int x = 0; x < _boardState.GetLength(0); ++x)
            {
                for (int y = 0; y < _boardState.GetLength(1); ++y)
                {
                    if (capturedPieces.Contains((x, y)))
                        continue;

                    // the opponent still has a royal piece
                    if (_boardState[x, y]?.Owner != CurrentPlayer && IsRoyalty(_boardState[x, y]?.Id ?? PieceIdentity.Pawn))
                        return false;
                }
            }

            return true;
        }

        // Public "debug" API: Set which piece (or no piece) at a board location.
        //   Raises "OnBoardChange" event if board state changes
        public void Debug_SetPiece(Piece piece, (int X, int Y) loc)
        {
            if (_boardState[loc.X, loc.Y] == piece)
                return;

            _boardState[loc.X, loc.Y] = piece;

            if (OnBoardChange != null)
            {
                var args = new BoardChangeEventArgs();
                OnBoardChange(this, args);
            }
        }

        // Public "debug" API: Set let the next player take a turn
        public void Debug_EndTurn() =>
            NextTurn();
    }

    public class PlayerChangeEventArgs : EventArgs
    {
        public Player OldPlayer { get; }

        public Player NewPlayer { get; }

        public PlayerChangeEventArgs(Player oldPlayer, Player newPlayer) => (OldPlayer, NewPlayer) = (oldPlayer, newPlayer);
    }

    public class BoardChangeEventArgs : EventArgs
    {
        public BoardChangeEventArgs() { }
    }
}