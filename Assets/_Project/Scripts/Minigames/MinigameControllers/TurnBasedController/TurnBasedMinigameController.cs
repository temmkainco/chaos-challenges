using Core;
using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Players act one at a time. Each turn has a time limit; when it runs out <see cref="OnTurnTimedOut"/> acts
/// for the player, so an AFK player can never stall the round. Eliminated players are frozen; the last one
/// standing wins and the rest place in reverse elimination order.
/// </summary>
public abstract class TurnBasedMinigameController : MinigameController
{
    public event Action<PlayerRef, bool> OnTurnChanged;
    [Networked] public PlayerRef CurrentTurn { get; set; }
    [Networked] public TickTimer TurnTimer { get; private set; }

    [SerializeField, Tooltip("Seconds per turn; 0 = no limit")] protected float _turnDuration = 12f;

    public float TurnTimeRemaining => TurnTimer.RemainingTime(Runner) ?? 0f;
    public bool HasTurnTimer => _turnDuration > 0f;

    protected List<PlayerRef> _alivePlayers = new();

    private readonly List<PlayerRef> _eliminationOrder = new();

    protected override void StartGame()
    {
        base.StartGame();

        if (!HasStateAuthority) return;

        _eliminationOrder.Clear();
        InitializeAlivePlayers();
        PickFirstTurn();
    }

    protected virtual void InitializeAlivePlayers()
    {
        _alivePlayers = ConnectedPlayers().ToList();
    }

    public override void HandlePlayerLeft(PlayerRef player)
    {
        if (IsGameActive)
            EliminatePlayer(player);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority || !IsGameActive || !TurnTimer.Expired(Runner))
            return;

        TurnTimer = TickTimer.None;
        OnTurnTimedOut();
    }

    /// <summary>Host only. The current player ran out of time; by default the turn passes on.</summary>
    protected virtual void OnTurnTimedOut() => AdvanceTurn();

    /// <summary>Optional extra HUD line (e.g. mines left).</summary>
    public virtual string GetStatusText() => null;

    protected void EliminatePlayer(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        int index = _alivePlayers.IndexOf(player);
        if (index < 0) return;

        bool wasTheirTurn = CurrentTurn == player;
        _alivePlayers.RemoveAt(index);
        _eliminationOrder.Add(player);
        SetPlayerInput(player, false);

        Debug.Log($"[Elimination] {player} eliminated. " +
                  $"Remaining: {_alivePlayers.Count}");

        if (_alivePlayers.Count <= 1)
        {
            if (_alivePlayers.Count == 1)
                _eliminationOrder.Add(_alivePlayers[0]);

            EndGame();
            return;
        }

        // The next player in order now sits at the eliminated player's index.
        if (wasTheirTurn)
            SetTurn(_alivePlayers[index % _alivePlayers.Count], false);
    }

    /// <summary>Ends the round; the last player standing places first.</summary>
    protected virtual void EndGame()
    {
        TurnTimer = TickTimer.None;
        var placements = Enumerable.Reverse(_eliminationOrder).ToList();
        FinishRound(placements);
    }

    protected virtual void PickFirstTurn()
    {
        int index = DeterministicRandom.Next(0, _alivePlayers.Count);
        Debug.Log($"First turn: {_alivePlayers[index]}");
        SetTurn(_alivePlayers[index], true);
    }

    protected void AdvanceTurn()
    {
        if (_alivePlayers.Count == 0) return;

        int index = _alivePlayers.IndexOf(CurrentTurn);
        SetTurn(_alivePlayers[(index + 1) % _alivePlayers.Count], false);
    }

    private void SetTurn(PlayerRef player, bool isFirst)
    {
        CurrentTurn = player;
        TurnTimer = HasTurnTimer ? TickTimer.CreateFromSeconds(Runner, _turnDuration) : TickTimer.None;
        RPC_OnTurnChanged(player, isFirst);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnTurnChanged(PlayerRef player, bool isFirst)
    {
        OnTurnChanged?.Invoke(player, isFirst);
    }
}
