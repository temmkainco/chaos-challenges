using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeaderboardController : NetworkBehaviour
{
    [Networked] private float LeaderboardTimer { get; set; }
    [Networked] private bool IsShowingLeaderboard { get; set; }

    [SerializeField] private float _displayDuration = 7f;
    [SerializeField] private float _finalDisplayDuration = 12f;
    [SerializeField] private int _maxSlots = 6;

    public event Action<List<LeaderboardEntry>> OnLeaderboardShown;
    public event Action OnLeaderboardHidden;

    private List<LeaderboardEntry> _cachedEntries;
    private bool _isCurrentlyShowing;
    private bool _dataReceived;
    private NetworkGameManager _gameManager;
    private bool _cachedIsFinal;
    public override void Spawned()
    {
        _gameManager = FindFirstObjectByType<NetworkGameManager>();
    }
    public override void Render()
    {
        if (IsShowingLeaderboard && _dataReceived && !_isCurrentlyShowing)
        {
            if (OnLeaderboardShown == null) return;

            _isCurrentlyShowing = true;
            OnLeaderboardShown.Invoke(_cachedEntries);
        }
    }

    public void RequestCurrentState(Action<List<LeaderboardEntry>> onShown)
    {
        if (_isCurrentlyShowing && _cachedEntries != null)
            onShown?.Invoke(_cachedEntries);
    }

    public void ShowLeaderboard(int[] minigameScores, bool isLastMinigame = false)
    {
        if (!HasStateAuthority) return;

        var entries = BuildEntries(minigameScores, isLastMinigame);

        IsShowingLeaderboard = true;
        LeaderboardTimer = isLastMinigame ? _finalDisplayDuration : _displayDuration;

        RPC_ShowLeaderboard(
            entries.Select(e => e.PlayerName).ToArray(),
            entries.Select(e => e.MinigameScore).ToArray(),
            entries.Select(e => e.TotalScore).ToArray(),
            entries.Select(e => e.Rank).ToArray(),
            entries.Select(e => e.IsEmpty).ToArray(),
            entries.Select(e => e.IsMinigameWinner).ToArray(),
            entries.Select(e => e.IsOverallWinner).ToArray(),
            isLastMinigame
        );
    }

    private List<LeaderboardEntry> BuildEntries(int[] minigameScores, bool isLastMinigame)
    {
        int playerCount = minigameScores.Length;

        int minigameWinnerSlot = Array.IndexOf(minigameScores, minigameScores.Max());

        int overallWinnerSlot = -1;
        if (isLastMinigame)
        {
            int highestTotal = -1;
            for (int i = 0; i < playerCount; i++)
            {
                int total = _gameManager.GetTotalScore(i);
                if (total > highestTotal)
                {
                    highestTotal = total;
                    overallWinnerSlot = i;
                }
            }
        }

        var entries = new List<LeaderboardEntry>();
        for (int i = 0; i < playerCount; i++)
        {
            entries.Add(new LeaderboardEntry
            {
                PlayerName = _gameManager.GetPlayerName(i),
                MinigameScore = minigameScores[i],
                TotalScore = _gameManager.GetTotalScore(i),
                IsEmpty = false,
                IsMinigameWinner = i == minigameWinnerSlot,
                IsOverallWinner = i == overallWinnerSlot,
            });
        }

        var ranked = entries.OrderByDescending(e => e.TotalScore).ToList();
        for (int i = 0; i < ranked.Count; i++)
            ranked[i].Rank = i + 1;

        while (ranked.Count < _maxSlots)
        {
            ranked.Add(new LeaderboardEntry
            {
                IsEmpty = true,
                PlayerName = "",
                MinigameScore = 0,
                TotalScore = 0,
                Rank = 0,
            });
        }

        return ranked;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsShowingLeaderboard) return;

        LeaderboardTimer -= Runner.DeltaTime;

        if (LeaderboardTimer <= 0f)
        {
            IsShowingLeaderboard = false;
            RPC_HideLeaderboard();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowLeaderboard(
        string[] names, int[] minigameScores, int[] totalScores, int[] ranks,
        bool[] isEmpty, bool[] isMinigameWinner, bool[] isOverallWinner,
        bool isLastMinigame)
    {
        _cachedIsFinal = isLastMinigame;
        _cachedEntries = new List<LeaderboardEntry>();
        for (int i = 0; i < names.Length; i++)
        {
            _cachedEntries.Add(new LeaderboardEntry
            {
                PlayerName = names[i],
                MinigameScore = minigameScores[i],
                TotalScore = totalScores[i],
                Rank = ranks[i],
                IsEmpty = isEmpty[i],
                IsMinigameWinner = isMinigameWinner[i],
                IsOverallWinner = isOverallWinner[i],
            });
        }

        _dataReceived = true;
        _isCurrentlyShowing = false;
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideLeaderboard()
    {
        _dataReceived = false;
        _isCurrentlyShowing = false;
        _cachedEntries = null;
        OnLeaderboardHidden?.Invoke();
    }
}