using Xunit;
using Xunit.Abstractions;

using System;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore.SignalR;

using ShogiComms;
using ShogiClient;
using ShogiEngine;

namespace ServerTest
{
    public sealed class MyFixture : IDisposable
    {
        readonly Process serverProcess;

        public MyFixture()
        {
            serverProcess = Process.Start(new ProcessStartInfo()
            {
                Arguments = "start",
                WorkingDirectory = "C:\\src\\taikyoku\\ShogiServerless",
                FileName = "func",
                CreateNoWindow = false,
                UseShellExecute = true
            }) ?? throw new Exception("process failed to start");
        }

        public void Dispose() => serverProcess?.Close();
    }

    public class UnitTest1 : IClassFixture<MyFixture>
    {
        private static int TIMEOUT { get => Debugger.IsAttached ? int.MaxValue : 10000; }

        private readonly ITestOutputHelper output;

        public UnitTest1(ITestOutputHelper output) => this.output = output;

        // Test that RequestOpenGames simply runs
        [Fact]
        public void TestRequestOpenGames1()
        {
            using var c = new Connection();
            Assert.True(c.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("requesting all game info...");
            Assert.True(c.RequestAllOpenGameInfo().Wait(TIMEOUT));
            output.WriteLine("...request returned (but not validated)");
        }

        // Test that RequestOpenGames returns back some valid data
        [Fact]
        public void TestRequestOpenGames2()
        {
            using var c = new Connection();
            Assert.True(c.ConnectAsync().Wait(TIMEOUT));

            // creating some new games
            const int GAME_COUNT = 10;
            Guid[] newGames = new Guid[10];
            output.WriteLine($"creating {GAME_COUNT} new games...");
            for (int i = 0; i < GAME_COUNT; ++i)
            {
                Assert.True(c.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
                {
                    output.WriteLine($"new game '{c.GameId}' created; player is '{c.PlayerId}'");
                    Assert.NotEqual(c.GameId, Guid.Empty);
                    Assert.NotEqual(c.PlayerId, Guid.Empty);
                    newGames[i] = c.GameId;
                }).Wait(TIMEOUT));
            }

            output.WriteLine("requesting all open game info...");
            Assert.True(c.RequestAllOpenGameInfo().ContinueWith(t =>
            {
                var list = t.Result;
                output.WriteLine($"game data received with {list.Count()} items");
                foreach (var gameId in newGames)
                {
                    output.WriteLine($"validating {gameId}");
                    Assert.NotNull(list.SingleOrDefault(g => g.GameId == gameId));
                }

            }).Wait(TIMEOUT));
            output.WriteLine("...validation complete");
        }

        [Fact]
        public void TestEcho()
        {
            using AutoResetEvent receivedEvent = new(false);
            string echoMessage = "";

            using var c = new Connection();
            c.OnReceiveEcho += (sender, e) => { echoMessage = e.Message; receivedEvent.Set(); };

            Assert.True(c.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("testing echo...");
            Assert.True(c.Echo("test message").Wait(TIMEOUT));
            output.WriteLine("...request returned");

            output.WriteLine("waiting on response...");
            Assert.True(receivedEvent.WaitOne(TIMEOUT));
            output.WriteLine($"...echo received: '{echoMessage}'");
        }

        [Fact]
        public void TestReceiveGameStart()
        {
            using AutoResetEvent receivedEvent = new(false);
            TaikyokuShogi game = new();

            using var c = new Connection(Guid.NewGuid(), Guid.NewGuid(), PlayerColor.Black);
            c.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"...game start received: '{e}'");
                Assert.Equal(e.GameInfo.GameId, c.GameId);
                Assert.Equal(e.PlayerId, c.PlayerId);
                Assert.True(e.Game.BoardStateEquals(game));
                receivedEvent.Set();
            };

            Assert.True(c.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("testing game start (via debug API)...");
            bool success = c.TestGameStart(new TaikyokuShogi()).Wait(TIMEOUT);
            Assert.True(success);
            output.WriteLine("...request returned");

            Console.WriteLine("waiting on response...");
            success = receivedEvent.WaitOne(TIMEOUT);
            Assert.True(success);
            Console.WriteLine("...done!");
        }

        [Fact]
        public void TestCreateGame()
        {
            using var c = new Connection();
            Assert.True(c.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("testing new game...");
            Assert.True(c.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
            {
                output.WriteLine($"new game '{c.GameId}' created; player is '{c.PlayerId}'");
            }).Wait(TIMEOUT));
        }

        [Fact]
        public void TestJoinGame()
        {
            using AutoResetEvent startEvent1 = new(false);
            using AutoResetEvent startEvent2 = new(false);
            TaikyokuShogi? game1 = null;
            TaikyokuShogi? game2 = null;

            using var c1 = new Connection();
            c1.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for initiating player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                Assert.Equal(c1.GameId, e.GameInfo.GameId);
                Assert.Equal(c1.PlayerId, e.PlayerId);
                Assert.NotEqual(c1.PlayerId, Guid.Empty);
                Assert.NotNull(e.Game);
                game1 = e.Game;
                startEvent1.Set();
            };
            Assert.True(c1.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("creating a new game...");
            Assert.True(c1.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
            {
                output.WriteLine($"new game '{c1.GameId}' created; player is '{c1.PlayerId}'");
                c1.SetGameInfo(c1.GameId, c1.PlayerId, PlayerColor.Black);
            }).Wait(TIMEOUT));

            Assert.NotEqual(c1.GameId, Guid.Empty);
            Assert.NotEqual(c1.PlayerId, Guid.Empty);

            using var c2 = new Connection(c1.GameId, Guid.Empty, PlayerColor.Black);
            c2.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for joining player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                Assert.Equal(c2.GameId, e.GameInfo.GameId);
                Assert.Equal(c2.PlayerId, e.PlayerId);
                Assert.NotEqual(c2.PlayerId, Guid.Empty);
                Assert.NotNull(e.Game);
                game2 = e.Game;
                startEvent2.Set();
            };
            Assert.True(c2.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("joining new game...");
            Assert.True(c2.JoinGame("other-player").Wait(TIMEOUT));

            output.WriteLine("waiting for game start events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { startEvent1, startEvent2 }, TIMEOUT));
            output.WriteLine("...game started for both players");

            Assert.True(game1?.BoardStateEquals(game2 ?? throw new NullReferenceException()));
        }

        // Helper Function that setups a game and with two Connections attached
        private (Connection, Connection) SetupGame()
        {
            using AutoResetEvent startEvent1 = new(false);
            using AutoResetEvent startEvent2 = new(false);

            var c1 = new Connection();
            c1.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for initiating player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                startEvent1.Set();
            };
            Assert.True(c1.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("creating a new game...");
            Assert.True(c1.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
            {
                output.WriteLine($"new game '{c1.GameId}' created; player is '{c1.PlayerId}'");
            }).Wait(TIMEOUT));

            output.WriteLine("joining a second playerto the game...");
            var c2 = new Connection(c1.GameId, Guid.Empty, PlayerColor.Black);
            c2.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for joining player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                startEvent2.Set();
            };
            Assert.True(c2.ConnectAsync().Wait(TIMEOUT));

            Assert.True(c2.JoinGame("other-player").Wait(TIMEOUT));

            output.WriteLine("waiting for game start events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { startEvent1, startEvent2 }, TIMEOUT));
            output.WriteLine("...game started for both players");

            return (c1, c2);
        }

        [Fact]
        public void TestMakeMove()
        {
            using AutoResetEvent e1 = new(false);
            using AutoResetEvent e2 = new(false);

            TaikyokuShogi? game1 = null;
            TaikyokuShogi? game2 = null;

            var (c1, c2) = SetupGame();
            using var closeC1 = c1;
            using var closeC2 = c2;

            c1.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"game updated for player1: '{e.GameId}'");
                game1 = e.Game;
                e1.Set();
            };

            c2.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"game updated for player2: '{e.GameId}'");
                game2 = e.Game;
                e2.Set();
            };

            output.WriteLine("black attemps move...");
            Assert.True(c1.RequestMove((5, 24), (6, 23), null, false).Wait(TIMEOUT));
            output.WriteLine("...move completed");

            output.WriteLine("waiting for game update events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { e1, e2 }, TIMEOUT));
            output.WriteLine("...both games updated");

            output.WriteLine("white attemps move...");
            Assert.True(c2.RequestMove((13, 10), (13, 11), null, false).Wait(TIMEOUT));
            output.WriteLine("...move completed");

            output.WriteLine("waiting for game update events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { e1, e2 }, TIMEOUT));
            output.WriteLine("...both games updated");

            Assert.True(game1?.BoardStateEquals(game2 ?? throw new NullReferenceException()) ?? false);
            Assert.Equal(PlayerColor.Black, game1?.CurrentPlayer);
        }

        [Fact]
        public void TestBadMove()
        {
            using AutoResetEvent e1 = new(false);
            using AutoResetEvent e2 = new(false);

            TaikyokuShogi? game1 = null;
            TaikyokuShogi? game2 = null;

            var (c1, c2) = SetupGame();
            using var closeC1 = c1;
            using var closeC2 = c2;

            c1.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"game updated for player1: '{e.GameId}'");
                game1 = e.Game;
                e1.Set();
            };

            c2.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"game updated for player2: '{e.GameId}'");
                game2 = e.Game;
                e2.Set();
            };

            output.WriteLine("black attemps _illegal_ move (will cause game end)...");
            Assert.True(c1.RequestMove((5, 24), (5, 25), null, false).Wait(TIMEOUT));
            output.WriteLine("...move completed");

            output.WriteLine("waiting for game update events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { e1, e2 }, TIMEOUT));
            output.WriteLine("...both games updated");

            Assert.True(game1?.BoardStateEquals(game2 ?? throw new NullReferenceException()) ?? false);
            Assert.Null(game1?.CurrentPlayer);
            Assert.Equal(PlayerColor.White, game1?.Winner);
            Assert.Equal(GameEndType.IllegalMove, game1?.Ending);
        }

        [Fact]
        public void TestResign()
        {
            using AutoResetEvent e1 = new(false);
            using AutoResetEvent e2 = new(false);

            TaikyokuShogi? game1 = null;
            TaikyokuShogi? game2 = null;

            var (c1, c2) = SetupGame();
            using var closeC1 = c1;
            using var closeC2 = c2;

            c1.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"game updated for player1: '{e.GameId}'");
                game1 = e.Game;
                e1.Set();
            };

            c2.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"game updated for player2: '{e.GameId}'");
                game2 = e.Game;
                e2.Set();
            };

            output.WriteLine("black attemps move...");
            Assert.True(c1.RequestMove((5, 24), (6, 23), null, false).Wait(TIMEOUT));
            output.WriteLine("...move completed");

            output.WriteLine("waiting for game update events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { e1, e2 }, TIMEOUT));
            output.WriteLine("...both games updated");

            output.WriteLine("initating reignation for black....");
            Assert.True(c1.ResignGame().Wait(TIMEOUT));

            output.WriteLine("waiting for game update events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { e1, e2 }, TIMEOUT));
            output.WriteLine("...both games updated");

            Assert.True(game1?.BoardStateEquals(game2 ?? throw new NullReferenceException()) ?? false);
            Assert.Null(game1?.CurrentPlayer);
            Assert.Equal(PlayerColor.White, game1?.Winner);
            Assert.Equal(GameEndType.Resignation, game1?.Ending);
        }

        [Fact]
        public void TestDisconnect()
        {
            using AutoResetEvent e1 = new(false);

            var (c1, c2) = SetupGame();

            c2.OnReceiveGameDisconnect += (sender, e) =>
            {
                output.WriteLine($"player2 received disconnect message: '{e.GameId}'");
                e1.Set();
            };

            output.WriteLine($"closing client connection for player 1");
            c1.Dispose();

            Assert.True(e1.WaitOne(TIMEOUT));

            c2.Dispose();
        }

        // Test scenarios around moving connection from game1 to game2
        [Fact]
        public void TestSecondConnect()
        {
            using AutoResetEvent gAp2DisconnectEvent = new(false);
            using AutoResetEvent gBp2DisconnectEvent = new(false);
            using AutoResetEvent gBp2ReconnectEvent = new(false);
            using AutoResetEvent gBp1UpdateEvent = new(false);
            using AutoResetEvent gBp2UpdateEvent = new(false);

            var (c1, c2) = SetupGame();
            var (c3, c4) = SetupGame();
            using var closeC1 = c1;
            using var closeC2 = c2;
            using var closeC3 = c3;
            using var closeC4 = c4;

            Guid gameA = c1.GameId;
            Guid playerA1 = c1.PlayerId;
            Guid playerA2 = c2.PlayerId;

            Guid gameB = c3.GameId;
            Guid playerB1 = c3.PlayerId;
            Guid playerB2 = c4.PlayerId;

            c1.OnReceiveGameDisconnect += (sender, e) => Assert.True(false);
            c2.OnReceiveGameDisconnect += (sender, e) =>
            {
                output.WriteLine($"c1 received disconnect message: '{e.GameId}'");
                Assert.Equal(e.GameId, gameA);
                gAp2DisconnectEvent.Set();
            };
            c3.OnReceiveGameDisconnect += (sender, e) => Assert.True(false);
            c4.OnReceiveGameDisconnect += (sender, e) =>
            {
                output.WriteLine($"c4 received disconnect message: '{e.GameId}'");
                Assert.Equal(e.GameId, gameB);
                gBp2DisconnectEvent.Set();
            };

            c1.OnReceiveGameReconnect += (sender, e) => Assert.True(false);
            c2.OnReceiveGameReconnect += (sender, e) => Assert.True(false);
            c3.OnReceiveGameReconnect += (sender, e) => Assert.True(false);
            c4.OnReceiveGameReconnect += (sender, e) =>
            {
                output.WriteLine($"c4 received reconect message: '{e.GameId}'");
                Assert.Equal(e.GameId, gameB);
                gBp2ReconnectEvent.Set();
            };

            c1.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"c1 received game update: '{e.GameId}'");
                Assert.Equal(e.GameId, gameB);
                gBp1UpdateEvent.Set();
            };
            c2.OnReceiveGameUpdate += (sender, e) => Assert.True(false);
            c3.OnReceiveGameUpdate += (sender, e) => Assert.True(false);
            c4.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"c4 received game update: '{e.GameId}'");
                Assert.Equal(e.GameId, gameB);
                gBp2UpdateEvent.Set();
            };

            output.WriteLine($"changing connection1 from Game A, Player 1 to Game B, Player 1");
            c1.SetGameInfo(gameB, playerB1, PlayerColor.Black);
            Assert.True(c1.RequestRejoinGame().Wait(TIMEOUT));

            output.WriteLine("waiting for game disconnect events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { gAp2DisconnectEvent, gBp2DisconnectEvent }, TIMEOUT));
            output.WriteLine("...both games updated");

            output.WriteLine("waiting for game reconnect event....");
            Assert.True(gBp2ReconnectEvent.WaitOne(TIMEOUT));
            output.WriteLine("...both games updated");

            output.WriteLine("attempting move via stale connection ...");
            Assert.ThrowsAsync<TaskCanceledException>(() => c3.RequestMove((5, 24), (6, 23), null, false));
            output.WriteLine("...move failed (as expected)");

            output.WriteLine("black attemps _illegal_ move...");
            Assert.True(c1.RequestMove((5, 24), (6, 23), null, false).Wait(TIMEOUT));
            output.WriteLine("...move completed");

            output.WriteLine("waiting for game update events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { gBp1UpdateEvent, gBp2UpdateEvent }, TIMEOUT));
            output.WriteLine("...both games updated");
        }

        // Test scenarios around moving connection from game1 to game2
        [Fact]
        public void TestLateJoins()
        {
            using AutoResetEvent gAp2DisconnectEvent = new(false);
            using AutoResetEvent gBp2DisconnectEvent = new(false);
            using AutoResetEvent gBp2ReconnectEvent = new(false);
            using AutoResetEvent gBp1UpdateEvent = new(false);
            using AutoResetEvent gBp2UpdateEvent = new(false);

            var (c1, c2) = SetupGame();
            using var closeC1 = c1;
            using var closeC2 = c2;

            output.WriteLine("joining a third player to the game...");
            var c3 = new Connection(c1.GameId, c2.PlayerId, PlayerColor.White);
            c3.OnReceiveGameStart += (sender, e) =>
            {
                Assert.True(false);
            };
            Assert.True(c3.ConnectAsync().Wait(TIMEOUT));

            var ex = Assert.Throws<AggregateException>(() => c3.JoinGame("third-wheel").Wait(TIMEOUT));
            Assert.IsType<HubException>(ex.InnerException);
            Assert.Equal(ex.InnerException?.Message, string.Format(HubExceptions.OpenGameNotFound, c1.GameId));
        }

        // Test cancel a game that has been opened but not joined
        [Fact]
        public void TestStartAndCancel()
        {
            using AutoResetEvent startEvent = new(false);

            using var c1 = new Connection();
            c1.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for initiating player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                startEvent.Set();
            };
            Assert.True(c1.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("creating a new game...");
            Assert.True(c1.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
            {
                output.WriteLine($"new game '{c1.GameId}' created; player is '{c1.PlayerId}'");
            }).Wait(TIMEOUT));

            Assert.True(c1.CancelGame().Wait(TIMEOUT));
        }
        
        // Test cancel a game that has already been joined
        [Fact]
        public void TestCancelOfJoinedGame()
        {
            var (c1, c2) = SetupGame();
            using var closeC1 = c1;
            using var closeC2 = c2;

            var ex = Assert.Throws<AggregateException>(() => c1.CancelGame().Wait(TIMEOUT));
            Assert.IsType<HubException>(ex.InnerException);
            Assert.Equal(ex.InnerException?.Message, string.Format(HubExceptions.OpenGameNotFound, c1.GameId));
        }

        // Test joining a game that has been canceled
        [Fact]
        public void TestJoinOfCancledGame()
        {
            using AutoResetEvent startEvent1 = new(false);
            using AutoResetEvent startEvent2 = new(false);

            using var c1 = new Connection();
            c1.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for initiating player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                startEvent1.Set();
            };
            Assert.True(c1.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("creating a new game...");
            Assert.True(c1.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
            {
                output.WriteLine($"new game '{c1.GameId}' created; player is '{c1.PlayerId}'");
            }).Wait(TIMEOUT));

            output.WriteLine("canceling the game...");
            Assert.True(c1.CancelGame().Wait(TIMEOUT));

            output.WriteLine("joining a second playerto the game...");
            using var c2 = new Connection(c1.GameId, Guid.Empty, PlayerColor.Black);
            c2.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for joining player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                startEvent2.Set();
            };
            Assert.True(c2.ConnectAsync().Wait(TIMEOUT));

            var ex = Assert.Throws<AggregateException>(() => c2.JoinGame("other-player").Wait(TIMEOUT));
            Assert.IsType<HubException>(ex.InnerException);
            Assert.Equal(ex.InnerException?.Message, string.Format(HubExceptions.OpenGameNotFound, c1.GameId));
        }
    }
}