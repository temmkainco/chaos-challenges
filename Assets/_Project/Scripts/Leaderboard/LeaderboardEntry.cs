using System;

[Serializable]
public class LeaderboardEntry
{
    public string PlayerName;
    public int MinigameScore;
    public int TotalScore;
    public int Rank;
    public bool IsEmpty;
    public bool IsMinigameWinner;
    public bool IsOverallWinner;
}