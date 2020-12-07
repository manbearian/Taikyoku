using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

using ShogiEngine;

namespace EngineTest
{
    public class MoveValidation
    {

        [Fact]
        public void NoPiece()
        {
            var game = new TaikyokuShogi(); // empty board
            var loc = (17, 17);
            Assert.Null(game.GetPiece(loc));
            Assert.Throws<InvalidOperationException>(() => game.MakeMove(loc, Movement.ComputeMove(loc, Movement.Up, 1).Value));
        }

        [Fact]
        public void OtherPlayersPiece()
        {
            var game = new TaikyokuShogi();
            var testPiece = new Piece(Player.White, PieceIdentity.Queen);
            var startLoc = (17, 17);
            game.Debug_SetPiece(testPiece, startLoc);
            Assert.Throws<InvalidOperationException>(() => game.MakeMove(startLoc, Movement.ComputeMove(startLoc, Movement.Up, 1).Value));
        }

        private void ValidateMoves(Piece testPiece, (int, int) startLoc, HashSet<(int, int)> validMoves, Dictionary<(int, int), Piece> otherPieces = null)
        {
            // test all the squares on the baord
            for (int x = 0; x < TaikyokuShogi.BoardWidth; ++x)
            {
                for (int y = 0; y < TaikyokuShogi.BoardHeight; ++y)
                {
                    var game = new TaikyokuShogi();
                    game.Debug_SetPiece(testPiece, startLoc);
                    if (otherPieces != null)
                    {
                        foreach (var kvp in otherPieces)
                        {
                            game.Debug_SetPiece(kvp.Value, kvp.Key);
                        }
                    }

                    var newLoc = (x, y);
                    var capturePiece = game.GetPiece(newLoc);

                    if (validMoves.Contains(newLoc))
                    {
                        game.MakeMove(startLoc, newLoc);
                        Assert.Null(game.GetPiece(startLoc));
                        Assert.Equal(testPiece, game.GetPiece(newLoc));

                        if (otherPieces != null)
                        {
                            foreach (var kvp in otherPieces.Where(kvp => kvp.Key != newLoc))
                            {
                                Assert.Equal(kvp.Value, game.GetPiece(kvp.Key));
                            }
                        }
                    }
                    else
                    {
                        Assert.Throws<InvalidOperationException>(() => game.MakeMove(startLoc, newLoc));
                        Assert.Equal(testPiece, game.GetPiece(startLoc));
                        Assert.Equal(capturePiece, game.GetPiece(newLoc));

                        if (otherPieces != null)
                        {
                            foreach (var kvp in otherPieces)
                            {
                                Assert.Equal(kvp.Value, game.GetPiece(kvp.Key));
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void Pawn()
        {
            var startLoc = (17, 17);
            var testPiece = new Piece(Player.Black, PieceIdentity.Pawn);

            // validate can only move up one square
            var validMoves = new HashSet<(int, int)>();
            validMoves.Add(Movement.ComputeMove(startLoc, Movement.Up, 1).Value);
            ValidateMoves(testPiece, startLoc, validMoves);

            var upOne = Movement.ComputeMove(startLoc, Movement.Up, 1).Value;

            // capture opponent piece
            ValidateMoves(testPiece, startLoc, validMoves,
                new Dictionary<(int, int), Piece>() { { upOne, new Piece(Player.White, PieceIdentity.King) } });

            // cannot capture your own piece
            ValidateMoves(testPiece, startLoc, new HashSet<(int, int)>(),
                new Dictionary<(int, int), Piece>() { { upOne, new Piece(Player.Black, PieceIdentity.King) } });
        }

        [Fact]
        public void RangeMove()
        {
            var startLoc = (12, 25); // random, non-centered location
            var testPiece = new Piece(Player.Black, PieceIdentity.Queen);

            var validMoves = new HashSet<(int X, int Y)>();

            // Can move any number of squares in any direction
            // compute this seperately from game engine
            foreach (var direction in Movement.OrthoganalDirectrions.Concat(Movement.DiagnalDirectrions))
            {
                for (int i = 1; i < Movement.Unlimited; ++i)
                {
                    var newLoc = Movement.ComputeMove(startLoc, direction, i);
                    if (newLoc == null)
                        break;
                    validMoves.Add(newLoc.Value);
                }
            }

            ValidateMoves(testPiece, startLoc, validMoves);

            validMoves.Clear();

            var otherPieces = new Dictionary<(int, int), Piece>();

            // scatter some pieces around the board - can capture in all directions
            for (int i = 0; i < Movement.DirectionCount; ++i)
            {
                otherPieces.Add(Movement.ComputeMove(startLoc, i, i + 1).Value, new Piece(Player.White, PieceIdentity.King));

                for (int j = 0; j <= i; ++j)
                {
                    var newLoc = Movement.ComputeMove(startLoc, i, j + 1);
                    if (newLoc == null)
                        break;
                    validMoves.Add(newLoc.Value);
                }
            }

            validMoves.Clear();
            otherPieces.Clear();

            // scatter some pieces around the board - cannot capture in any direction, blocked by own pieces
            for (int i = 0; i < Movement.DirectionCount; ++i)
            {
                otherPieces.Add(Movement.ComputeMove(startLoc, i, i + 1).Value, new Piece(Player.Black, PieceIdentity.King));
                otherPieces.Add(Movement.ComputeMove(startLoc, i, i + 2).Value, new Piece(Player.White, PieceIdentity.King));

                for (int j = 0; j < i; ++j)
                {
                    var newLoc = Movement.ComputeMove(startLoc, i, j + 1);
                    if (newLoc == null)
                        break;
                    validMoves.Add(newLoc.Value);
                }
            }

            // capture opponent piece
            ValidateMoves(testPiece, startLoc, validMoves, otherPieces);
        }
    }
}
