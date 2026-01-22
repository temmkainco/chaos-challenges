using Fusion;
using Zenject;
using UnityEngine;
using System;

public class LobbyManager : NetworkBehaviour
{
    [Networked, Capacity(6)]
    public NetworkDictionary<PlayerRef, bool> ReadyStates => default;

    public event Action AllPlayersReadyEvent;
    public event Action<PlayerRef, bool> PlayerReadyChangedEvent;

    [Inject] private LobbyPlayerSpawner _spawner;

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        ResyncWithPlayers();
        _spawner.OnPlayersChangedEvent += ResyncWithPlayers;
    }

    private void OnDestroy()
    {
        if (_spawner != null)
            _spawner.OnPlayersChangedEvent -= ResyncWithPlayers;
    }

    public void ToggleReady(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        if (!ReadyStates.ContainsKey(player))
            return;

        bool newValue = !ReadyStates[player];
        ReadyStates.Set(player, newValue);
        RPC_NotifyPlayerReadyChanged(player, newValue);
        Debug.Log($"LobbyManager: {player} ready = {newValue}");
        PlayerReadyChangedEvent?.Invoke(player, newValue);
        CheckAllReady();
    }

    private void ResyncWithPlayers()
    {
        if (!HasStateAuthority)
            return;

        foreach (var kvp in _spawner.Players)
        {
            if (!ReadyStates.ContainsKey(kvp.Key))
                ReadyStates.Add(kvp.Key, false);
        }

        foreach (var kvp in ReadyStates)
        {
            if (!_spawner.Players.ContainsKey(kvp.Key))
                ReadyStates.Remove(kvp.Key);
        }

        CheckAllReady();
    }

    private void CheckAllReady()
    {
        if (ReadyStates.Count <= 1)
            return;

        foreach (var kvp in ReadyStates)
        {
            if (!kvp.Value)
                return;
        }

        Debug.Log("ALL PLAYERS READY");
        AllPlayersReadyEvent?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerReadyChanged(PlayerRef player, bool ready)
    {
        PlayerReadyChangedEvent?.Invoke(player, ready);
    }
}
