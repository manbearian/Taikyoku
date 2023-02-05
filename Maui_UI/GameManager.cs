using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using ShogiClient;
using ShogiEngine;

namespace MauiUI;


internal class GameChangeEventArgs : EventArgs
{
    public GameChangeEventArgs() { }
}

internal class GameManager : BindableObject
{
    //
    // Events
    //

    public delegate void GameChangeHandler(object sender, GameChangeEventArgs e);
    public event GameChangeHandler? OnGameChange;

    //
    // Bindabe Proprerties
    //

    public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(GameManager), null, BindingMode.OneWay, propertyChanged: OnConnectionChanged);

    public Connection? Connection
    {
        get => (Connection?)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    // Standard Properties

    public TaikyokuShogi Game { get; private set; } = new TaikyokuShogi(); // initialize with fake game to make nullable easier

    public Guid? LocalGameId { get; private set; }

    public string OpponentName { get; private set; } = string.Empty;

    public bool IsLocalGame { get; private set; }

    public PlayerColor? CurrentPlayer { get => Game.CurrentPlayer; }

    private static void OnConnectionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var me = (GameManager)bindable;

        if (oldValue is Connection oldConnection)
            oldConnection.OnReceiveGameUpdate -= me.Connection_OnReceiveGameUpdate;
        if (newValue is Connection newConnection)
            newConnection.OnReceiveGameUpdate += me.Connection_OnReceiveGameUpdate;
    }

    private void Connection_OnReceiveGameUpdate(object sender, ReceiveGameUpdateEventArgs e)
    {
        Contract.Assert(!IsLocalGame);
        Game = e.Game;
        OnGameChange?.Invoke(this, new GameChangeEventArgs());
    }

    public void SetLocalGame(TaikyokuShogi game, Guid? localGameId)
    {
        (Game, LocalGameId, IsLocalGame, OpponentName) = (game, localGameId, true, string.Empty);
        OnGameChange?.Invoke(this, new GameChangeEventArgs());
    }

    public void SetNetworkGame(TaikyokuShogi game, string opponentName)
    {
        (Game, LocalGameId, IsLocalGame, OpponentName) = (game, null, false, opponentName);
        OnGameChange?.Invoke(this, new GameChangeEventArgs());
    }

    public async Task MakeMove((int X, int Y) startLoc, (int X, int Y) endLoc, (int X, int Y)? midLoc = null, bool promote = false)
    {
        Game.MakeMove(startLoc, endLoc, midLoc, promote);
        OnGameChange?.Invoke(this, new GameChangeEventArgs());

        if (!IsLocalGame)
        {
            Contract.Assert(Connection is not null);
            await Connection.RequestMove(startLoc, endLoc, midLoc, promote);
        }
    }

    public async Task Resign()
    {
        if (CurrentPlayer is null)
            return;

        Contract.Assert(IsLocalGame || Connection is not null);

        var resigningPlayer = IsLocalGame ? CurrentPlayer.Value : Connection?.Color;
        Contract.Assert(resigningPlayer is not null);
        Game.Resign(resigningPlayer.Value);
        OnGameChange?.Invoke(this, new GameChangeEventArgs());

        if (!IsLocalGame)
        {
            Contract.Assert(Connection is not null);
            await Connection.ResignGame();
        }
    }
}
