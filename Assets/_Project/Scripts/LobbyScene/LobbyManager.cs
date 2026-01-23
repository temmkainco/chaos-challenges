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

    [Inject(Id = "NetworkGameManager")] 
    private NetworkPrefabRef _networkGameManagerPrefab;

    [Inject] private LobbyPlayerSpawner _spawner;
    [Inject] private MinigameSceneDatabaseSO _minigameSceneDatabase;

    private NetworkGameManager _networkGameManager;
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
        if (ReadyStates.Count == 0)
            return;

        foreach (var kvp in ReadyStates)
        {
            if (!kvp.Value)
                return;
        }

        AllPlayersReadyEvent?.Invoke();

        SpawnNetworkGameManager();
        StartNetworkGame();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerReadyChanged(PlayerRef player, bool ready)
    {
        PlayerReadyChangedEvent?.Invoke(player, ready);
    }

    private void SpawnNetworkGameManager()
    {
        if(_networkGameManager != null)
            return;

        var gameFlowControllerObject = Runner.Spawn(
            _networkGameManagerPrefab,
            Vector3.zero,
            Quaternion.identity
        );

        _networkGameManager = gameFlowControllerObject.GetComponent<NetworkGameManager>();
        _networkGameManager.Initialize(_minigameSceneDatabase);
    }

    private void StartNetworkGame()
    {
        if (!HasStateAuthority)
            return;

        _networkGameManager.StartMatch();
    }
}
