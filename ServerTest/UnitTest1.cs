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
            Thread.Sleep(6500);
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
                    output.WriteLine($"new game '{c.GameId}' created; player is '{c.PlayerId}'");
                    Assert.NotEqual(c.GameId, Guid.Empty);
                    Assert.NotEqual(c.PlayerId, Guid.Empty);
                    newGames[i] = c.GameId;
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
        public void TestReceiveGameStart()
        {
            AutoResetEvent receivedEvent = new(false);
            TaikyokuShogi game = new();

            using var c = new Connection(Guid.NewGuid(), Guid.NewGuid(), PlayerColor.Black);
            c.OnReceiveGameStart += (sender, e) => {
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
            AutoResetEvent startEvent1 = new(false);
            AutoResetEvent startEvent2 = new(false);
            TaikyokuShogi? game1 = null;
            TaikyokuShogi? game2 = null;

            using var c1 = new Connection();
            c1.OnReceiveGameStart += (sender, e) => {
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

        [Fact]
        public void TestMakeMove()
        {
            AutoResetEvent startEvent1 = new(false);
            AutoResetEvent startEvent2 = new(false);
            AutoResetEvent updateEvent1 = new(false);
            AutoResetEvent updateEvent2 = new(false);

            TaikyokuShogi? game1 = null;
            TaikyokuShogi? game2 = null;

            using var c1 = new Connection();
            c1.OnReceiveGameStart += (sender, e) => {
                output.WriteLine($"game start for initiating player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                game1 = e.Game;
                startEvent1.Set();
            };
            c1.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"game updated for player1: '{e.GameId}'");
                game1 = e.Game;
                updateEvent1.Set();
            };
            Assert.True(c1.ConnectAsync().Wait(TIMEOUT));

            output.WriteLine("creating a new game...");
            Assert.True(c1.RequestNewGame("test-player", true, new TaikyokuShogi()).ContinueWith(t =>
            {
                output.WriteLine($"new game '{c1.GameId}' created; player is '{c1.PlayerId}'");
            }).Wait(TIMEOUT));

            output.WriteLine("joining a second playerto the game...");
            using var c2 = new Connection(c1.GameId, Guid.Empty, PlayerColor.Black);
            c2.OnReceiveGameStart += (sender, e) =>
            {
                output.WriteLine($"game start for joining player: '{e.GameInfo.GameId}'/'{e.PlayerId}'");
                game2 = e.Game;
                startEvent2.Set();
            };
            c2.OnReceiveGameUpdate += (sender, e) =>
            {
                output.WriteLine($"game updated for player2: '{e.GameId}'");
                game2 = e.Game;
                updateEvent2.Set();
            };
            Assert.True(c2.ConnectAsync().Wait(TIMEOUT));

            Assert.True(c2.JoinGame("other-player").Wait(TIMEOUT));

            output.WriteLine("waiting for game start events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { startEvent1, startEvent2 }, TIMEOUT));
            output.WriteLine("...game started for both players");

            output.WriteLine("black attemps _illegal_ move (0,0) -> (1,1)...");
            Assert.True(c1.RequestMove((0, 0), (1, 1), null, false).Wait(TIMEOUT));
            output.WriteLine("...move completed");

            output.WriteLine("waiting for game update events....");
            Assert.True(WaitHandle.WaitAll(new WaitHandle[] { updateEvent1, updateEvent2 }, TIMEOUT));
            output.WriteLine("...both games updated");

            Assert.True(game1?.BoardStateEquals(game2 ?? throw new NullReferenceException()) ?? false);
            Assert.True(game1?.Winner == PlayerColor.White);
            Assert.True(game1?.Ending == GameEndType.IllegalMove);
        }
    }
}