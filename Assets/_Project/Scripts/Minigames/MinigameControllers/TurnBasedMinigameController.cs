// TurnBasedMinigameController.cs
using Core;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class TurnBasedMinigameController : MinigameController
{
    [Networked] public PlayerRef CurrentTurn { get; set; }

    protected List<PlayerRef> _alivePlayers = new();

    // Host-only: tracks elimination order. First eliminated = index 0.
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
        if (!HasStateAuthority) return;

        _alivePlayers.Remove(player);
        _eliminationOrder.Add(player); // earliest eliminated goes first in the list

        Debug.Log($"[Elimination] {player} eliminated. " +
                  $"Remaining: {_alivePlayers.Count}");

        if (_alivePlayers.Count <= 1)
        {
            // Last survivor wins — add them at the end of the order
            if (_alivePlayers.Count == 1)
                _eliminationOrder.Add(_alivePlayers[0]);

            AssignEliminationScores();
            EndGame();
            return;
        }

        AdvanceTurn();
    }

    /// <summary>
    /// Converts elimination order into scores and pushes them to each client.
    /// First eliminated → lowest score, last standing → highest score.
    /// 
    /// Example with 4 players, base score 100, step 100:
    ///   Rank 1 (1st eliminated) → 100
    ///   Rank 2                  → 200
    ///   Rank 3                  → 300
    ///   Rank 4 (winner)         → 400
    /// </summary>
    private void AssignEliminationScores()
    {
        int totalPlayers = _eliminationOrder.Count;
        const int baseScore = 100;
        const int step = 100;

        Debug.Log($"=== {_minigameDefinition.Id} Elimination Results ===");

        for (int rank = 0; rank < _eliminationOrder.Count; rank++)
        {
            PlayerRef player = _eliminationOrder[rank];
            int score = baseScore + rank * step;
            int slot = GameManager.GetSlotForPlayer(player);

            Debug.Log($"  #{rank + 1} (place {totalPlayers - rank}) — {player} → {score} pts (slot {slot})");

            // Host writes directly — no client round-trip needed
            if (slot >= 0)
                GameManager.AddToGlobalScore(slot, score);
        }
    }
    protected override void EndGame()
    {
        // Scores already written to GlobalScores by AssignEliminationScores()
        // So we skip the local score submission flow entirely
        IsGameActive = false;
        RPC_OnGameEnd();
        SetPlayersInput(false);
        Debug.Log($"Minigame {_minigameDefinition.Id} ended!");
        // DO NOT call base.EndGame() — that would trigger RPC_SubmitScore with _localScore = 0
    }

    protected void AdvanceTurn()
    {
        if (_alivePlayers.Count == 0) return;

        int index = _alivePlayers.IndexOf(CurrentTurn);
        index = (index + 1) % _alivePlayers.Count;
        CurrentTurn = _alivePlayers[index];
    }
}