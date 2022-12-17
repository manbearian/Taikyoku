using Xunit;


using ShogiClient;
using System;
using System.Threading;
using System.Security.Principal;
using System.Diagnostics;
using Xunit.Abstractions;
using ShogiEngine;
using System.Threading.Tasks;
using System.Linq;

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

            // wait for server to launch
            Thread.Sleep(6000);
        }

        public void Dispose() => serverProcess?.Close();
    }


    public class UnitTest1 : IClassFixture<MyFixture>
    {
        const int TIMEOUT = 5000;

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
                    var (gameId, playerId) = t.Result;
                    output.WriteLine($"new game '{gameId}' created; player is '{playerId}'");
                    newGames[i] = gameId;
                }).Wait(TIMEOUT));
            }

            output.WriteLine("requesting all open game info...");
            Assert.True(c.RequestAllOpenGameInfo().ContinueWith(t => {
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
            AutoResetEvent receivedEvent = new(false);
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
        public void TestGameStart()
        {
            AutoResetEvent receivedEvent = new(false);
            Guid gameId = Guid.Empty;
            TaikyokuShogi? game = null;

            using var c = new Connection();
            c.OnReceiveGameStart += (sender, e) => { gameId = e.GameId; game = e.Game; receivedEvent.Set(); };

            Assert.True(c.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("testing game start...");
            bool success = c.TestGameStart(new TaikyokuShogi()).Wait(TIMEOUT);
            Assert.True(success);
            output.WriteLine("...request returned");

            Console.WriteLine("waiting on response...");
            success = receivedEvent.WaitOne(TIMEOUT);
            Assert.True(success);
            Assert.NotEqual(gameId, Guid.Empty);
            Assert.NotNull(game);

            output.WriteLine($"...game start received: '{gameId}', 'mc={game?.MoveCount}' and 'cp={game?.CurrentPlayer}'");
        }

        [Fact]
        public void TestCreateGame()
        { 
            using var c = new Connection();
            Assert.True(c.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("testing new game...");
            Assert.True(c.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
            {
                var pair = t.Result;
                output.WriteLine($"new game '{pair.GameId}' created; player is '{pair.PlayerId}'");
            }).Wait(TIMEOUT));
        }

        [Fact]
        public void TestJoinGame()
        {
            AutoResetEvent startEvent1 = new(false);
            AutoResetEvent startEvent2 = new(false);
            TaikyokuShogi? game1 = null;
            TaikyokuShogi? game2 = null;
            Guid gameId = Guid.Empty;
            Guid playerId = Guid.Empty;

            using var c1 = new Connection();
            c1.OnReceiveGameStart += (sender, e) => {
                output.WriteLine($"game start for initiating player: '{e.GameId}'/'{e.PlayerId}'");
                Assert.Equal(gameId, e.GameId);
                Assert.Equal(playerId, e.PlayerId);
                Assert.NotNull(e.Game);
                game1 = e.Game;
                startEvent1.Set();
            };

            using var c2 = new Connection();
            c2.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for joining player: '{e.GameId}'/'{e.PlayerId}'");
                Assert.Equal(gameId, e.GameId);
                Assert.NotEqual(Guid.Empty, e.PlayerId);
                Assert.NotNull(e.Game);
                game2 = e.Game;
                startEvent2.Set();
            };

            Assert.True(c1.ConnectAsync().Wait(TIMEOUT));
            Assert.True(c2.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("creating a new game...");
            Assert.True(c1.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
            {
                (gameId, playerId) = t.Result;
                output.WriteLine($"new game '{gameId}' created; player is '{playerId}'");
            }).Wait(TIMEOUT));

            Assert.NotEqual(gameId, Guid.Empty);
            Assert.NotEqual(playerId, Guid.Empty);

            output.WriteLine("joining new game...");
            Assert.True(c2.JoinGame(gameId, "other-player").Wait(TIMEOUT));

            output.WriteLine("waiting for game start events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { startEvent1, startEvent2 }, TIMEOUT));
            output.WriteLine("...game started for both players");

            Assert.True(game1?.BoardStateEquals(game2 ?? throw new NullReferenceException()));
        }
    }
}