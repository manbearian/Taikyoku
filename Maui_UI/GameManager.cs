using System.Diagnostics.Contracts;

using ShogiClient;
using ShogiEngine;

namespace MauiUI;


internal class GameChangeEventArgs : EventArgs
{
    public GameChangeEventArgs() { }
}

internal class GameManager
{
    //
    // Events
    //

    public delegate void GameChangeHandler(object sender, GameChangeEventArgs e);
    public event GameChangeHandler? OnGameChange;

    public TaikyokuShogi Game { get; private set; } = new TaikyokuShogi(); // initialize with fake game to make nullable easier

    public Guid? LocalGameId { get; private set; }

    public string OpponentName { get; private set; } = string.Empty;

    public bool IsLocalGame { get; private set; }

    private static Connection Connection { get => MainPage.Default.Connection; }

    public bool IsListening
    {
        set
        {
            void Connection_OnReceiveGameUpdate(object sender, ReceiveGameUpdateEventArgs e)
            {
                Contract.Assert(!IsLocalGame);
                Game = e.Game;
                OnGameChange?.Invoke(this, new GameChangeEventArgs());
            }

            if (value)
                Connection.OnReceiveGameUpdate += Connection_OnReceiveGameUpdate;
            else
                Connection.OnReceiveGameUpdate -= Connection_OnReceiveGameUpdate;
        }
    }

    public void SetLocalGame(TaikyokuShogi game, Guid? localGameId) =>
        (Game, LocalGameId, IsLocalGame, OpponentName) = (game, localGameId, true, string.Empty);

    public void SetNetworkGame(TaikyokuShogi game, string opponentName) =>
        (Game, LocalGameId, IsLocalGame, OpponentName) = (game, null, false, opponentName);
 
    public async Task MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null, bool promote = false)
    {
        Contract.Assert(Game is not null);

        Game.MakeMove(startLoc, endLoc, midLoc, promote);
        OnGameChange?.Invoke(this, new GameChangeEventArgs());

        if (!IsLocalGame)
            await Connection.RequestMove(startLoc, endLoc, midLoc, promote);
    }
}
