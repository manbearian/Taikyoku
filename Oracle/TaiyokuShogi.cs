using System;
using System.Collections.Generic;
using System.Linq;


namespace Oracle
{
    public enum Player
    {
        Black,
        White
    }

    public class IllegalMoveException : Exception { }

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

        private readonly (Player, PieceIdentity)?[,] _boardState = new (Player, PieceIdentity)?[BoardWidth, BoardHeight];
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

        public (Player Owner, PieceIdentity Id)? GetPiece((int X, int Y) loc) => _boardState[loc.X, loc.Y];

        public TaiyokuShogiOptions Options { get; }

        private void SetInitialBoard()
        {
            _boardState.SetInitialState();

            if (OnBoardChange != null)
            {
                var args = new BoardChangeEventArgs();
                OnBoardChange(this, args);
            }
        }
        // move the piece at startLoc to endLoc
        //   midLoc is for area-moves
        public void MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null)
        {
            var piece = GetPiece(startLoc);

            if (piece == null || piece.Value.Owner != CurrentPlayer)
                throw new IllegalMoveException();

            var moves = this.GetLegalMoves(CurrentPlayer, piece.Value.Id, startLoc, midLoc).Where(move => move.Loc == endLoc);

            if (!moves.Any())
                throw new IllegalMoveException();

            if (midLoc != null)
            {
                if (!moves.Any(move => move.Type == MoveType.Area || move.Type == MoveType.Igui))
                    throw new IllegalMoveException();

                // capture any piece that got run over by the area-move
                _boardState[midLoc.Value.X, midLoc.Value.Y] = null;
            }

            if (moves.Any(move => move.Type == MoveType.RangedCapture))
            {
                int xCount = startLoc.X - endLoc.X;
                int yCount = startLoc.Y - endLoc.Y;

                // orthoganal or diagonal only
                if (xCount != 0 && yCount != 0 && Math.Abs(xCount) != Math.Abs(yCount))
                    throw new IllegalMoveException();

                var xMultiplier = xCount == 0 ? 0 : (xCount > 0 ? -1 : 1);
                var yMultiplier = yCount == 0 ? 0 : (yCount > 0 ? -1 : 1);
                for (int i = 0; i < Math.Max(Math.Abs(xCount), Math.Abs(yCount)); ++i)
                {
                    int x = startLoc.X + xMultiplier * i;
                    int y = startLoc.Y + yMultiplier * i;
                    _boardState[x, y] = null;
                }
            }

            // set new location, this has the effect of removing any piece that was there from the board
            _boardState[startLoc.X, startLoc.Y] = null;
            _boardState[endLoc.X, endLoc.Y] = piece;

            if (OnBoardChange != null)
            {
                var args = new BoardChangeEventArgs();
                OnBoardChange(this, args);
            }

            NextTurn();
        }

        public void NextTurn() => CurrentPlayer = (CurrentPlayer == Player.Black ? Player.White : Player.Black);

        public void Reset()
        {
            SetInitialBoard();
            CurrentPlayer = Player.Black;
        }


        public void Debug_AddPiece((Player Player, PieceIdentity Id) piece, (int X, int Y) loc)
        {
            if (_boardState[loc.X, loc.Y] != null)
                throw new IllegalMoveException();
            _boardState[loc.X, loc.Y] = piece;
        }

        public void Debug_RemovePiece((int X, int Y) loc)
        {
            if (_boardState[loc.X, loc.Y] == null)
                throw new IllegalMoveException();
            _boardState[loc.X, loc.Y] = null;
        }

        public void Debug_EndTurn()
        {
            NextTurn();
        }
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