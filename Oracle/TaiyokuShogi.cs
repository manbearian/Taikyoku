using System;
using System.Collections.Generic;
using System.Linq;


namespace Oracle
{
    public enum Player
    {
        White,
        Black
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

        public IEnumerable<(int X, int Y)> GetLegalMoves(Player player, PieceIdentity id, (int X, int Y) loc)
        {
            var legalMoves = new List<(int X, int Y)>();
            var movement = this.GetMovement(id);

            // add a move the list of legal moves
            // returns the location added if stepping can continue in the assocated direction
            (int X, int Y)? AddMove((int X, int Y) loc, int direction, int distance)
            {
                var moveAmount = player == Player.White ? distance : -distance;
                var (x, y) = direction switch
                {
                    Movement.Up => (loc.X, loc.Y - moveAmount),
                    Movement.Down => (loc.X, loc.Y + moveAmount),
                    Movement.Left => (loc.X - moveAmount, loc.Y),
                    Movement.Right => (loc.X + moveAmount, loc.Y),
                    Movement.UpLeft => (loc.X - moveAmount, loc.Y - moveAmount),
                    Movement.UpRight => (loc.X + moveAmount, loc.Y - moveAmount),
                    Movement.DownLeft => (loc.X - moveAmount, loc.Y + moveAmount),
                    Movement.DownRight => (loc.X + moveAmount, loc.Y + moveAmount),
                    _ => throw new NotSupportedException()
                };

                if (y < 0 || y >= BoardHeight)
                    return null;
                if (x < 0 || x >= BoardWidth)
                    return null;

                var existingPiece = _boardState[x, y];
                if (existingPiece.HasValue)
                {
                    if (existingPiece.Value.Item1 != player)
                        legalMoves.Add((x, y));
                    return null;
                }

                legalMoves.Add((x, y));
                return (x, y);
            }

            void AddSteps((int X, int Y) loc, int direction, int range) =>
                Enumerable.Range(1, range).FirstOrDefault(i => AddMove(loc, direction, i) == null);

            for (int direction = 0; direction < movement.StepRange.Length; ++direction)
            {
                AddSteps(loc, direction, movement.StepRange[direction]);
            }

            for (int direction = 0; direction < movement.JumpRange.Length; ++direction)
            {
                for (int i = 0; i < movement.JumpRange[direction].JumpDistances?.Length; ++i)
                {
                    var jumpLoc = AddMove(loc, direction, movement.JumpRange[direction].JumpDistances[i] + 1);
                    if (jumpLoc != null)
                    {
                        AddSteps(jumpLoc.Value, direction, movement.JumpRange[direction].RangeAfter);
                    }
                }
            }

            return legalMoves;
        }

        // move the piece at startLoc to endLoc
        //   midLoc is for area-moves
        public void MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null)
        {
            var piece = GetPiece(startLoc);

            if (piece == null || piece.Value.Owner != CurrentPlayer)
                throw new IllegalMoveException();

            if (!GetLegalMoves(CurrentPlayer, piece.Value.Id, startLoc).Contains(endLoc))
                throw new IllegalMoveException();

            if (midLoc != null)
            {
                // capture any piece that got run over by the area-move
                // todo: test if this is an area mover
                _boardState[midLoc.Value.X, midLoc.Value.Y] = null;
            }

            // todo: multi-capture for ranged-capture pieces

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

        public void NextTurn() => CurrentPlayer = (CurrentPlayer == Player.White ? Player.Black : Player.White);

        public void Reset()
        {
            SetInitialBoard();
            CurrentPlayer = Player.White;
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