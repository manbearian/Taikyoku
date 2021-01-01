using System;
using System.Collections.Generic;
using System.Text;

namespace ShogiEngine
{
    public class MoveRecorder
    {
        public class MoveDescription
        {
            public (int X, int Y) StartLoc { get; }

            public (int X, int Y) EndLoc { get; }

            public (int X, int Y)? MidLoc { get; }

            public PieceIdentity? PromotedFrom { get; }

            public IEnumerable<(Piece Piece, (int X, int Y) Location)> Captures { get; }

            public MoveDescription() { }

            public MoveDescription((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc, PieceIdentity? promotedFrom, IEnumerable<(Piece Piece, (int X, int Y) Location)> captures) =>
                (StartLoc, EndLoc, MidLoc, PromotedFrom, Captures) = (startLoc, endLoc, midLoc, promotedFrom, captures);
        }

        private Stack<MoveDescription> _moves =  new Stack<MoveDescription>();

        public IEnumerable<MoveDescription> Moves { get => _moves; }

        public int Count { get => _moves.Count; }

        public void PushMove(MoveDescription move) => _moves.Push(move);

        public void PushMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc, PieceIdentity? promotedFrom, IEnumerable<(Piece Piece, (int X, int Y) Location)> captures) => 
            PushMove(new MoveDescription(startLoc, endLoc, midLoc, promotedFrom, captures));

        public MoveDescription PopMove() =>_moves.Pop();
    }
}
