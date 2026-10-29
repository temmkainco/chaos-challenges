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

        if (!ConnectedPlayers().Any(p => !_finishOrder.Contains(p)))
            FinishRound(_finishOrder);
    }

    /// <summary>Finishers keep their order; everyone still racing ties for the places after them.</summary>
    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings()
    {
        var standings = _finishOrder.Select(p => (IReadOnlyList<PlayerRef>)new[] { p }).ToList();
        var stillRacing = ConnectedPlayers().Where(p => !_finishOrder.Contains(p)).ToList();
        if (stillRacing.Count > 0)
            standings.Add(stillRacing);
        return standings;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerFinished(PlayerRef player, int position)
    {
        OnPlayerFinished?.Invoke(player, position);
    }
}
