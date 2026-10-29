using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Turns match phase changes into leaderboard data for <see cref="LeaderboardUI"/>.
/// Round results stay hidden (names only); totals are shown only in the final reveal.
/// Reads the networked roster, so every peer builds the same entries without extra RPCs.
/// </summary>
public class LeaderboardController : NetworkBehaviour
{
    [SerializeField] private int _maxSlots = 6;

    /// <summary>Entries ordered for display, and whether this is the final reveal.</summary>
    public event Action<List<LeaderboardEntry>, bool> OnLeaderboardShown;
    public event Action OnLeaderboardHidden;

    private NetworkGameManager _gameManager;

    public float RemainingTime => _gameManager != null ? _gameManager.PhaseTimeRemaining : 0f;

    public override void Spawned()
    {
        _gameManager = NetworkGameManager.Instance;
        if (_gameManager != null)
            _gameManager.PhaseChanged += OnPhaseChanged;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_gameManager != null)
            _gameManager.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(MatchPhase phase)
    {
        switch (phase)
        {
            case MatchPhase.RoundOver:
                OnLeaderboardShown?.Invoke(BuildEntries(isFinal: false), false);
                break;
            case MatchPhase.FinalReveal:
                OnLeaderboardShown?.Invoke(BuildEntries(isFinal: true), true);
                break;
            default:
                OnLeaderboardHidden?.Invoke();
                break;
        }
    }

    private List<LeaderboardEntry> BuildEntries(bool isFinal)
    {
        var roster = _gameManager.Players.ToList();
        List<LeaderboardEntry> entries;

        if (isFinal)
        {
            int topScore = roster.Count > 0 ? roster.Max(r => r.Score) : 0;
            entries = roster
                .OrderByDescending(r => r.Score)
                .Select((r, i) => new LeaderboardEntry
                {
                    PlayerName = r.Name.ToString(),
                    MinigameScore = r.LastRoundPoints,
                    TotalScore = r.Score,
                    Rank = i + 1,
                    IsOverallWinner = r.Score == topScore,
                })
                .ToList();
        }
        else
        {
            entries = roster
                .Select(r => new LeaderboardEntry { PlayerName = r.Name.ToString(), HideScores = true })
                .ToList();
        }

        while (entries.Count < _maxSlots)
            entries.Add(new LeaderboardEntry { IsEmpty = true, PlayerName = "" });

        return entries;
    }
}
