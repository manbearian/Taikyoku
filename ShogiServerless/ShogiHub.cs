using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Azure;

using Azure.Storage.Queues;
using Azure.Storage.Blobs;
using Azure.Core.Extensions;

using ShogiEngine;
using ShogiComms;
using Microsoft.Extensions.Logging;
using Azure.Core;
using System.Text.Json;
using Microsoft.Azure.SignalR.Management;

namespace ShogiServerless
{
    public interface IShogiClient
    {
        Task ReceiveGameList(List<ClientGameInfo> list);

        Task ReceiveGameStart(string serializedGame, ClientGameInfo gameInfo, Guid playerId);

        Task ReceiveGameUpdate(string serializedGame, Guid gameId);

        // indicate the other player disconncted
        Task ReceiveGameDisconnect(Guid gameId);

        // indicate that the other player reconnected
        Task ReceiveGameReconnect(Guid gameId);

        Task Echo(string message);
    }

    public class ShogiHub : ServerlessHub<IShogiClient>
    {
        private static TableStorage TableStorage { get; } = new ();

        private static readonly ConnectionMap _connectionMap = new ();

        // database of games looking for players
        private static readonly ConcurrentDictionary<Guid, OpenGameInfo> OpenGames = new ();

        public ShogiHub() : base() { }

        private async Task SignalDisconnectToOpponent(Guid gameId, Guid playerId, ILogger logger)
        {
            var gameInfo = await TableStorage.FindGame(gameId);
            if (gameInfo == null)
            {
                // game could have been removed, or not added yet, or whatever; just skip
                logger.LogInformation($"Game not found, skipping disconnect from '{gameId}'/'{playerId}' ");
                return;
            }

            var otherPlayerInfo = gameInfo.GetOtherPlayerInfo(playerId);

            if (otherPlayerInfo is not null &&
                    TryGetConnection(gameId, otherPlayerInfo.PlayerId, out var otherConnection, logger))
            {
                logger.LogInformation($"sending disconnect to '{otherConnection}' for '{gameId}'/'{otherPlayerInfo.PlayerId}'");
                await Clients.Client(otherConnection).ReceiveGameDisconnect(gameInfo.Id);
            }
        }

        private async Task UnmapConnection(string connectionId, ILogger logger)
        {
            logger.LogInformation($"unmapping connection '{connectionId}'");
            var stalePair = _connectionMap.UnmapConnection(connectionId);

            if (stalePair is not null)
            {
                Guid gameId = (Guid)stalePair?.GameId!;
                Guid playerId = (Guid)stalePair?.PlayerId!;
                await SignalDisconnectToOpponent(gameId, playerId, logger);
            }
        }

        private async Task MapConnection(string connectionId, Guid gameId, Guid playerId, ILogger logger)
        {
            logger.LogInformation($"mapping '{connectionId}' to '{gameId}/{playerId}'");

            var (oldConnection, oldGamePlayerPair) = _connectionMap.MapConnection(connectionId, gameId, playerId);

            if (oldGamePlayerPair is not null)
            {
                var oldGameId = (Guid)oldGamePlayerPair?.GameId!;
                var oldPlayerId = (Guid)oldGamePlayerPair?.PlayerId!;
                logger.LogInformation($"'{connectionId}' previously mapped to '{oldGameId}'/'{oldPlayerId}'");
                await SignalDisconnectToOpponent(oldGameId, oldPlayerId, logger);
            }

            if (oldConnection is not null)
            {
                logger.LogInformation($"'{gameId}'/'{playerId}' prevoiusly mapped to '{connectionId}'");
                await SignalDisconnectToOpponent(gameId, playerId, logger);

                logger.LogInformation($"closing stale connection '{oldConnection}'");
                await ClientManager.CloseConnectionAsync(oldConnection, "new connection created");
            }
        }

        private static bool TryGetConnection(Guid gameId, Guid playerId, out string connectionId, ILogger logger)
        {
            bool result = _connectionMap.TryGetConnection(gameId, playerId, out connectionId);
            logger.LogInformation(result ? $"mapped '{gameId}/{playerId}' to '{connectionId}'"
                : $"no connection map found for '{gameId}/{playerId}'");
            return result;
        }

        private static bool TryGetPlayer(string connectionId, out Guid gameId, out Guid playerId, ILogger logger)
        {
            bool result = _connectionMap.TryGetPlayer(connectionId, out gameId, out playerId);
            logger.LogInformation(result ? $"mapped '{connectionId}' to '{gameId}/{playerId}'"
                : $"no connection map found for '{connectionId}'");
            return result;
        }

        private static IEnumerable<ClientGameInfo> AllOpenGames() =>
            OpenGames.Values.Select(info => info.ToClientGameInfo()).ToList();


        [FunctionName(nameof(CreateGame))]
        public async Task<GamePlayerPair> CreateGame([SignalRTrigger] InvocationContext context,
            string playerName, bool asBlackPlayer, string existingGameSerialized, ILogger logger)
        {
            var game = JsonSerializer.Deserialize<TaikyokuShogi>(existingGameSerialized ?? "")
                ?? throw new HubException("failed to deserialize game state");

            OpenGameInfo gameInfo = new(game, playerName, asBlackPlayer ? PlayerColor.Black : PlayerColor.White);
            if (!OpenGames.TryAdd(gameInfo.GameId, gameInfo))
            {
                throw new HubException("failed to add game");
            }

            logger.LogInformation($"client '{context.ConnectionId}' creaging new game created with id '{gameInfo.GameId}'");

            logger.LogInformation("sending attached clients updated game list");
            await Clients.All.ReceiveGameList(AllOpenGames().ToList());

            await MapConnection(context.ConnectionId, gameInfo.GameId, gameInfo.WaitingPlayerInfo.PlayerId, logger);

            return new GamePlayerPair(gameInfo.GameId, gameInfo.WaitingPlayerInfo.PlayerId);
        }

        [FunctionName(nameof(RequestAllOpenGameInfo))]
        public async Task<IEnumerable<ClientGameInfo>> RequestAllOpenGameInfo([SignalRTrigger] InvocationContext context) =>
            await Task.Run(() => AllOpenGames());

        [FunctionName(nameof(RequestGameInfo))]
        public async Task<IEnumerable<ClientGameInfo>> RequestGameInfo([SignalRTrigger] InvocationContext context,
            NetworkGameRequestList requests)
        {
            var gameList = new ConcurrentBag<ClientGameInfo>();

            // query the table storage for all the requested games
            var tasks = new List<Task>();
            foreach (var request in requests.List)
            {
                tasks.Add(TableStorage.FindGame(request.GameId).
                    ContinueWith(t =>
                    {
                        var gameInfo = t.Result;
                        if (gameInfo is not null)
                            gameList.Add(gameInfo.ToClientGameInfo());
                    }));
            }

            return await Task.WhenAll(tasks.ToArray()).ContinueWith(t =>
            {
                return gameList as IEnumerable<ClientGameInfo>;
            });
        }

        [FunctionName(nameof(JoinGame))]
        public async Task JoinGame([SignalRTrigger] InvocationContext context, 
            Guid gameId, string playerName,
            ILogger logger)
        {
            logger.LogInformation($"client '{context.ConnectionId}' named '{playerName}' requesting to join game '{gameId}'");

            if (!OpenGames.TryRemove(gameId, out var openGameInfo))
            {
                logger.LogInformation($"'{gameId}' was not found in OpenGames list");
                throw new HubException($"game '{gameId}' is no longer available");
            }

            logger.LogInformation($"sending all clients updated open game list");
            await Clients.All.ReceiveGameList(AllOpenGames().ToList());

            var gameInfo = TableStorage.AddGame(new GameInfo(openGameInfo, playerName))
                ?? throw new HubException("Internal Storage Error: Unable to add game");

            logger.LogInformation($"'{gameId}' successfully moved from OpenGames to LiveGames");

            var oldPlayerInfo = openGameInfo.WaitingPlayerInfo;
            var newPlayerInfo = gameInfo.GetOtherPlayerInfo(oldPlayerInfo.PlayerId);

            logger.LogInformation($"mapping '{context.ConnectionId}' to '{gameId}' as '{newPlayerInfo}'");

            // add new connection to the map
            await MapConnection(context.ConnectionId, gameId, newPlayerInfo.PlayerId, logger);

            // signal other player game has started
            if (TryGetConnection(gameId, oldPlayerInfo.PlayerId, out var otherConnection, logger))
            {
                logger.LogInformation($"signal player '{oldPlayerInfo}' game start for '{gameId}'");
                await Clients.Client(otherConnection).
                    ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.ToClientGameInfo(), oldPlayerInfo.PlayerId);
            }

            // signal joining player game has started
            logger.LogInformation($"signal player '{newPlayerInfo}' game start for '{gameId}'");
            await Clients.Client(context.ConnectionId).
                ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.ToClientGameInfo(), newPlayerInfo.PlayerId);
        }

        [FunctionName(nameof(RejoinGame))]
        public async Task RejoinGame([SignalRTrigger] InvocationContext context,
            Guid gameId, Guid playerId,
            ILogger logger)
        {
            var gameInfo = await TableStorage.FindGame(gameId)
                ?? throw new HubException($"failed to find game: {gameId}");
            var otherPlayerInfo = gameInfo.GetOtherPlayerInfo(playerId);

            await MapConnection(context.ConnectionId, gameId, playerId, logger);

            if (otherPlayerInfo is not null)
            {
                logger.LogInformation($"other player for '{gameId}' is '{otherPlayerInfo.PlayerId}'");
                if (TryGetConnection(gameId, otherPlayerInfo.PlayerId, out var otherConnection, logger))
                {
                    logger.LogInformation($"signal other connection '{otherConnection}' there is a reconnect for '{gameId}'");
                    await Clients.Client(otherConnection).ReceiveGameReconnect(gameId);
                }
            }
            else
            {
                logger.LogInformation($"no other player connected to '{gameId}'");
            }

            logger.LogInformation($"signal this player there is a game start for '{gameId}'");
            await Clients.Client(context.ConnectionId).ReceiveGameStart(gameInfo.Game.ToJsonString(), gameInfo.ToClientGameInfo(), playerId);
        }

        // Record updated game state into persistant storage and notify any attached clients
        private async Task UpdateGame(GameInfo gameInfo,
            ILogger logger)
        {
            gameInfo = TableStorage.UpdateGame(gameInfo)
                ?? throw new HubException("Interal Server error: cannot record move");

            var blackPlayerId = gameInfo.BlackPlayer?.PlayerId ?? throw new HubException("game in bad state; missing black player");
            var whitePlayerId = gameInfo.WhitePlayer?.PlayerId ?? throw new HubException("game in bad state; missing white player");

            logger.LogInformation($"Black player is '{gameInfo.Id}/{blackPlayerId}'");
            logger.LogInformation($"White player is '{gameInfo.Id}/{whitePlayerId}'");

            if (TryGetConnection(gameInfo.Id, blackPlayerId, out var connectionBlack, logger))
            {
                logger.LogInformation($"Updating black player'{gameInfo.Id}/{blackPlayerId}' at '{connectionBlack}'");
                await Clients.Client(connectionBlack).ReceiveGameUpdate(gameInfo.Game.ToJsonString(), gameInfo.Id);
            }

            if (TryGetConnection(gameInfo.Id, whitePlayerId, out var connectionWhite, logger))
            {
                logger.LogInformation($"Updating white Player '{gameInfo.Id}/{whitePlayerId}' at '{connectionWhite}'");
                await Clients.Client(connectionWhite).ReceiveGameUpdate(gameInfo.Game.ToJsonString(), gameInfo.Id);
            }
        }

        [FunctionName(nameof(MakeMove))]
        public async Task MakeMove([SignalRTrigger] InvocationContext context,
            Location startLoc, Location endLoc, Location midLoc, bool promote,
            ILogger logger)
        {
            if (!TryGetPlayer(context.ConnectionId, out var gameId, out var playerId, logger))
                return;

            var gameInfo = await TableStorage.FindGame(gameId)
                ?? throw new HubException($"failed to find game: {gameId}");

            if (gameInfo.Game.CurrentPlayer != gameInfo.GetPlayerColor(playerId))
                throw new HubException("illegal move: wrong client requested the move");

            try
            {
                gameInfo.Game.MakeMove(((int, int))startLoc, ((int, int))endLoc, ((int, int)?)midLoc, promote);
                gameInfo.LastPlayed = DateTime.Now;
            }
            catch (InvalidOperationException e)
            {
                throw new HubException("invalid move: unable to complete move", e);
            }

            await UpdateGame(gameInfo, logger);
        }

        [FunctionName(nameof(Resign))]
        public async Task Resign([SignalRTrigger] InvocationContext context, ILogger logger)
        {
            if (!TryGetPlayer(context.ConnectionId, out var gameId, out var playerId, logger))
                return;

            var gameInfo = await TableStorage.FindGame(gameId)
                ?? throw new HubException($"failed to find game: {gameId}");

            gameInfo.Game.Resign(gameInfo.GetPlayerColor(playerId));
            gameInfo.LastPlayed = DateTime.Now;

            await UpdateGame(gameInfo, logger);
        }

#if DEBUG
        [FunctionName(nameof(Echo))]
        public async Task Echo([SignalRTrigger] InvocationContext context, string message, ILogger logger)
        {
            logger.LogInformation($"echoing '{message}' back to '{context.ConnectionId}'");
            await Clients.Client(context.ConnectionId).Echo(message);
        }

        [FunctionName(nameof(TestGameStart))]
        public async Task TestGameStart([SignalRTrigger] InvocationContext context,
            Guid gameId, Guid playerId, string serializedGame,
            ILogger logger)
        {
            var game = serializedGame.ToTaikyokuShogi();
            logger.LogInformation($"testing GameStart message for '{context.ConnectionId}'");

            var info = new ClientGameInfo()
            {
                GameId = gameId,
                BlackName = "blackPlayerFakeName",
                WhiteName = "whitePlayerFakeName"
            };
            await Clients.Client(context.ConnectionId).ReceiveGameStart(game.ToJsonString(), info, playerId);
        }
#endif

        [FunctionName(nameof(OnConnected))]
        public void OnConnected([SignalRTrigger] InvocationContext context, ILogger logger)
        {
            logger.LogInformation($"{context.ConnectionId} has connected");
        }

        [FunctionName(nameof(OnDisconnected))]
        public async Task OnDisconnected([SignalRTrigger] InvocationContext context, ILogger logger)
        {
            await UnmapConnection(context.ConnectionId, logger);
            logger.LogInformation($"{context.ConnectionId} has disconnected");
        }

        [FunctionName("negotiate")]
        public async Task<SignalRConnectionInfo> Negotiate([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req)
            => await NegotiateAsync(new NegotiationOptions());
    }
}