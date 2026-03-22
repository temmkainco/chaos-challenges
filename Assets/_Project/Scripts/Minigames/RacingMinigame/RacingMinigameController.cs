using Fusion;
using System;
using UnityEngine;

public class RacingMinigameController : MinigameController
{
    [Networked] private int PlayersFinished { get; set; }

    private int[] _finishScores;
    private int _expectedPlayers;

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        PlayersFinished = 0;
        _expectedPlayers = GameManager.ExpectedPlayerCount;
        _finishScores = new int[_expectedPlayers];
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PlayerFinished(PlayerRef player)
    {
        if (!IsGameActive) return;

        int slot = GameManager.GetSlotForPlayer(player);
        if (slot < 0) return;

        int score = Mathf.Max(100, 400 - PlayersFinished * 100);
        _finishScores[slot] = score;
        GameManager.AddToGlobalScore(slot, score);

        PlayersFinished++;
        Debug.Log("Players Finished" + PlayersFinished);
        RPC_OnPlayerFinished(player, PlayersFinished);

        if (PlayersFinished >= _expectedPlayers)
            EndGame();
    }
    protected override void EndGame()
    {
        IsGameActive = false;
        SetPlayersInput(false);

        if (HasStateAuthority)
        {
            if (_leaderboard == null)
                _leaderboard = FindFirstObjectByType<LeaderboardController>();

            bool isLast = GameManager.IsLastMinigame();
            _leaderboard.OnLeaderboardHidden += OnLeaderboardDone;
            _leaderboard.ShowLeaderboard(_finishScores, isLast);
        }
    }

    private void OnLeaderboardDone()
    {
        _leaderboard.OnLeaderboardHidden -= OnLeaderboardDone;
        if (HasStateAuthority)
            RPC_OnGameEnd();
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerFinished(PlayerRef player, int position)
    {
        OnPlayerFinished?.Invoke(player, position);
    }

    public event Action OnRaceStart;
    public event Action<PlayerRef, int> OnPlayerFinished;
}