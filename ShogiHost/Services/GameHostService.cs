using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShogiHost
{
    public class GameHostService : GameHost.GameHostBase
    {
        private readonly ILogger<GameHostService> _logger;
        public GameHostService(ILogger<GameHostService> logger)
        {
            _logger = logger;
        }

        ShogiEngine.TaikyokuShogi _game;

        private GameState GetState() => new GameState() {
            State = Google.Protobuf.ByteString.CopyFrom(_game.Serialize())
        };

        public override Task<GameState> MakeMove(Move request, ServerCallContext context)
        {
            if (request.Mid.Loc.Count > 1)
                throw new Exception("illegal message recieved");

            var mid = request.Mid.Loc.Count == 0 ? null as (int, int)? : (request.Mid.Loc[0].X, request.Mid.Loc[0].Y);
            _game.MakeMove((request.Start.X, request.Start.Y), (request.End.X, request.End.Y), mid, request.Promote);
            return Task.FromResult(GetState());
        }

        public override Task<GameState> StartGame(Nothing request, ServerCallContext context)
        {
            _game = new ShogiEngine.TaikyokuShogi(ShogiEngine.TaikyokuShogiOptions.None);
            return Task.FromResult(GetState());
        }
    }
}
