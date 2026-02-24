using Core;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class TurnBasedMinigameController : MinigameController
{
    [Networked] public PlayerRef CurrentTurn { get; set; }

    protected List<PlayerRef> _alivePlayers = new();

    protected override void StartGame()
    {
        base.StartGame();

        if (!HasStateAuthority)
            return;

        InitializeAlivePlayers();
        PickFirstTurn();
    }

    protected virtual void InitializeAlivePlayers()
    {
        _alivePlayers = Runner.ActivePlayers.ToList();
    }

    protected virtual void PickFirstTurn()
    {
        int index = DeterministicRandom.Next(0, _alivePlayers.Count);
        CurrentTurn = _alivePlayers[index];
        Debug.Log($"First turn: {CurrentTurn}");
    }

    protected void EliminatePlayer(PlayerRef player)
    {
        _alivePlayers.Remove(player);

        if (_alivePlayers.Count <= 1)
        {
            EndGame();
            return;
        }

        AdvanceTurn();
    }

    protected void AdvanceTurn()
    {
        if (_alivePlayers.Count == 0)
            return;

        int index = _alivePlayers.IndexOf(CurrentTurn);
        index = (index + 1) % _alivePlayers.Count;
        CurrentTurn = _alivePlayers[index];
    }
}