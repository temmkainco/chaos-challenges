using Core;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class TurnBasedMinigameController : MinigameController
{
    [Networked] public PlayerRef CurrentTurn { get; set; }

    protected List<PlayerRef> _alivePlayers = new();

    private readonly List<PlayerRef> _eliminationOrder = new();
    private int[] _lastMinigameScores;
    private bool _wasLastMinigame;
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
        _eliminationOrder.Add(player);

        Debug.Log($"[Elimination] {player} eliminated. " +
                  $"Remaining: {_alivePlayers.Count}");

        if (_alivePlayers.Count <= 1)
        {
            if (_alivePlayers.Count == 1)
                _eliminationOrder.Add(_alivePlayers[0]);

            AssignEliminationScores();
            EndGame();
            return;
        }

        AdvanceTurn();
    }

    private void AssignEliminationScores()
    {
        int totalPlayers = GameManager.ExpectedPlayerCount;
        const int baseScore = 100;
        const int step = 100;

        _lastMinigameScores = new int[totalPlayers];

        Debug.Log($"=== {_minigameDefinition.Id} Elimination Results ===");

        for (int rank = 0; rank < _eliminationOrder.Count; rank++)
        {
            PlayerRef player = _eliminationOrder[rank];
            int score = baseScore + rank * step;
            int slot = GameManager.GetSlotForPlayer(player);

            Debug.Log($"  #{rank + 1} — {player} → {score} pts (slot {slot})");

            if (slot >= 0)
            {
                GameManager.AddToGlobalScore(slot, score);
                _lastMinigameScores[slot] = score;
            }
        }
    }

    protected override void EndGame()
    {
        IsGameActive = false;
        SetPlayersInput(false);
        Debug.Log($"Minigame {_minigameDefinition.Id} ended!");

        if (HasStateAuthority)
        {
            if (_leaderboard == null)
                _leaderboard = FindFirstObjectByType<LeaderboardController>();

            if (_leaderboard == null)
            {
                Debug.LogError("LeaderboardController not found! Skipping leaderboard.");
                RPC_OnGameEnd();
                return;
            }

            _wasLastMinigame = GameManager.IsLastMinigame();
            _leaderboard.OnLeaderboardHidden += OnLeaderboardDone;
            _leaderboard.ShowLeaderboard(_lastMinigameScores, _wasLastMinigame);
        }
    }
    private void OnLeaderboardDone()
    {
        _leaderboard.OnLeaderboardHidden -= OnLeaderboardDone;
        if (HasStateAuthority)
            RPC_OnGameEnd();
    }

    protected void AdvanceTurn()
    {
        if (_alivePlayers.Count == 0) return;

        int index = _alivePlayers.IndexOf(CurrentTurn);
        index = (index + 1) % _alivePlayers.Count;
        CurrentTurn = _alivePlayers[index];
    }
}