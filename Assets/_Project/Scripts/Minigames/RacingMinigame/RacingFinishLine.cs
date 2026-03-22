using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class FinishLine : NetworkBehaviour
{
    [Inject] private RacingMinigameController _controller;

    private readonly HashSet<PlayerRef> _finishedPlayers = new();

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return; 
        if (!other.TryGetComponent<Player>(out var player)) return;

        PlayerRef playerRef = player.Object.InputAuthority;

        if (_finishedPlayers.Contains(playerRef)) return;

        _finishedPlayers.Add(playerRef);
        _controller.RPC_PlayerFinished(playerRef);
    }
}