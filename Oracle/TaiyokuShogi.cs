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

    public enum GameEndType
    {
        Checkmate,
        IllegalMove
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
        private (GameEndType, Player? winner)? _gameEnding;

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

        private Player OtherPlayer { get => CurrentPlayer == Player.Black ? Player.White : Player.Black; }

        public (GameEndType, Player? winner)? GameEnding
        {
            get => _gameEnding;
            
            private set
            {
                _gameEnding = value;
                if (OnGameEnd != null)
                {
                    var args = new GameEndEventArgs(value.Value.Item1, CurrentPlayer);
                    OnGameEnd(this, args);
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

        public delegate void GameEndHandler(object sender, GameEndEventArgs e);
        public event GameEndHandler OnGameEnd;

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
        //   return value indicates if the move was valid. Note that illegal moves are concerned valid, but will end the game.
        public bool MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null, bool promote = false)
        {
            bool IllegalMove()
            {
                GameEnding = (GameEndType.IllegalMove, OtherPlayer);
                return true;
            }

            bool InvalidMove() => false;

            var piece = GetPiece(startLoc);

            // nonsencial moves are invalid
            if (GameEnding != null || piece == null)
            {
                return InvalidMove();
            }

            // moving your oppontents piece is illegal
            if (piece.Owner != CurrentPlayer)
                return IllegalMove();

            var moves = this.GetLegalMoves(piece, startLoc, midLoc).Where(move => move.Loc == endLoc);

            if (!moves.Any())
                return IllegalMove();

            var capturedPieces = new List<(int X, int Y)>();

            if (midLoc != null)
            {
                if (!moves.Any(move => move.Type == MoveType.Area || move.Type == MoveType.Igui))
                    return IllegalMove();

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
                    return IllegalMove();

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
                return IllegalMove();

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

            if (OnBoardChange != null)
            {
                var args = new BoardChangeEventArgs();
                OnBoardChange(this, args);
            }

            if (IsCheckmate())
            {
                GameEnding = (GameEndType.Checkmate, CurrentPlayer);
                return true;
            }

            NextTurn();
            return true;

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

        // Public API: Reset the game to its initial state
        public void Reset()
        {
            SetInitialBoard();
            CurrentPlayer = Player.Black;
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

    public class GameEndEventArgs : EventArgs
    {
        public GameEndType Ending { get; }

        public Player? Winner { get; }

        public GameEndEventArgs(GameEndType gameEnding, Player? winner) => (Ending, Winner) = (gameEnding, winner);
    }
}