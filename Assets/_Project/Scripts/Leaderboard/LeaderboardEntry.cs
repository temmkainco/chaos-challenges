using System;
using UnityEngine;

[Serializable]
public class LeaderboardEntry
{
    public string PlayerName;
    /// <summary>The player's number and colour (their chip), or 0 / grey if they left.</summary>
    public int Number;
    public Color Color = Color.grey;
    public int MinigameScore;
    public int TotalScore;
    public int Rank;
    public bool IsEmpty;
    public bool IsMinigameWinner;
    public bool IsOverallWinner;
    /// <summary>Round results are secret until the final reveal.</summary>
    public bool HideScores;
    /// <summary>Round podium: the place in this round is shown, points and totals stay secret.</summary>
    public bool RoundPodium;
}