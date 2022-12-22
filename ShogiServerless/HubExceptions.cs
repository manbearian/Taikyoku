using System;

using Microsoft.AspNetCore.SignalR;

using ShogiComms;

namespace ShogiServerless
{
    // The HubException is marhshalled as as string only back to the client
    // This family of exceptions take advantage of this to pass safe information back to the client

    public class OpenGameNotFoundException : HubException
    {
        public Guid GameId { get; }

        public OpenGameNotFoundException(Guid gameId) :
            base(string.Format(HubExceptions.OpenGameNotFound, gameId)) => 
            GameId = gameId;
    }

    public class AddGameException : HubException
    {
        public Guid GameId { get; }
        
        public AddGameException(Guid gameId) :
            base(string.Format(HubExceptions.AddGameFailed, gameId)) =>
            GameId = gameId;
    }

    public class UpdateGameException : HubException
    {
        public Guid GameId { get; }
        
        public UpdateGameException(Guid gameId) :
            base(string.Format(HubExceptions.UpdateGameFailed, gameId)) =>
            GameId = gameId;
    }

    public class FindGameException : HubException
    {
        public Guid GameId { get; }

        public FindGameException(Guid gameId) :
            base(string.Format(HubExceptions.FindGameFailed, gameId)) =>
            GameId = gameId;
    }

    public class InvalidMoveException : HubException
    {
        public Guid GameId { get; }

        public InvalidMoveException(Guid gameId, ShogiEngine.InvalidMoveException ex) :
            base(string.Format(HubExceptions.InvalidMove, ex.Message, gameId), ex) =>
            GameId = gameId;
    }
}
