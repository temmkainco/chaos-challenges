using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RacingMinigameController : MinigameController
{
    [Networked] private int PlayersFinished { get; set; }

    private readonly List<PlayerRef> _finishOrder = new();

    public event Action<PlayerRef, int> OnPlayerFinished;

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        PlayersFinished = 0;
        _finishOrder.Clear();
    }

    /// <summary>Host only (the finish line is checked on the host).</summary>
    public void PlayerFinished(PlayerRef player)
    {
        if (!HasStateAuthority || !IsGameActive || _finishOrder.Contains(player))
            return;

        _finishOrder.Add(player);
        PlayersFinished = _finishOrder.Count;
        Debug.Log($"Player {player} finished #{PlayersFinished}");
        RPC_OnPlayerFinished(player, PlayersFinished);

        CheckRaceOver();
    }

    public override void HandlePlayerLeft(PlayerRef player)
    {
        CheckRaceOver();
    }

    private void CheckRaceOver()
    {
        if (!IsGameActive)
            return;

        bool anyoneStillRacing = Runner.ActivePlayers
            .Any(p => GameManager.IsConnectedPlayer(p) && !_finishOrder.Contains(p));

        if (!anyoneStillRacing)
            FinishRound(_finishOrder);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerFinished(PlayerRef player, int position)
    {
        OnPlayerFinished?.Invoke(player, position);
    }
}
