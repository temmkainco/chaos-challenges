using Core;
using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Spawns one player object per connected player and gives each a spawn point of their own.
/// During a match the point follows the player's roster slot, so everyone starts in the same spot every round.
/// </summary>
public abstract class BasePlayerSpawner : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public event Action OnPlayersChangedEvent;

    [SerializeField] protected NetworkPrefabRef _playerPrefab;
    [SerializeField] protected Transform[] _spawnPoints;

    [Inject] protected DiContainer _container;

    [Networked, Capacity(6)]
    public NetworkDictionary<PlayerRef, Player> Players => default;

    /// <summary>Minigames keep players frozen until the round starts.</summary>
    protected virtual bool LockControlsOnSpawn => false;

    private readonly Dictionary<PlayerRef, int> _assignedSpawns = new();

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        if (_spawnPoints == null || _spawnPoints.Length == 0)
            Debug.LogError($"{GetType().Name}: no spawn points assigned; players spawn at the origin.");

        foreach (var playerRef in Runner.ActivePlayers)
        {
            if (!Players.ContainsKey(playerRef) && Runner.GetPlayerObject(playerRef) == null)
                PlayerJoined(playerRef);
        }
    }

    /// <summary>Host only. Puts the player back on their spawn point (respawn zones).</summary>
    public void RequestSpawn(Player player)
    {
        if (!HasStateAuthority) return;

        var spawn = GetSpawnPoint(player.Object.InputAuthority);
        if (player.TryGetComponent<NetworkCharacterController>(out var controller))
        {
            controller.Teleport(spawn.position, Quaternion.identity);
            controller.Velocity = Vector3.zero;
        }
    }

    public virtual void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (Players.ContainsKey(player))
        {
            Debug.LogWarning($"Player {player} already spawned, skipping");
            return;
        }

        Debug.Log($"Spawning player {player}");

        var spawn = GetSpawnPoint(player);
        NetworkObject playerObject = Runner.Spawn(_playerPrefab, spawn.position, Quaternion.identity, player);
        var playerBehaviour = playerObject.GetComponent<Player>();
        playerBehaviour.ControlsLocked = LockControlsOnSpawn;
        _container.InjectGameObject(playerBehaviour.gameObject);
        Players.Add(player, playerBehaviour);
        Runner.SetPlayerObject(player, playerObject);
        RPC_UpdateList();
    }

    public virtual void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        _assignedSpawns.Remove(player);

        if (Players.TryGet(player, out Player playerBehaviour))
        {
            Debug.Log($"Despawning player {player}");
            Players.Remove(player);
            if (playerBehaviour != null)
                Runner.Despawn(playerBehaviour.Object);
            RPC_UpdateList();
        }
    }

    private (Vector3 position, Quaternion rotation) GetSpawnPoint(PlayerRef player)
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
            return (Vector3.zero, Quaternion.identity);

        if (!_assignedSpawns.TryGetValue(player, out int index))
        {
            index = PickSpawnIndex(player);
            _assignedSpawns[player] = index;
        }

        var point = _spawnPoints[index];
        return (point.position, point.rotation);
    }

    private int PickSpawnIndex(PlayerRef player)
    {
        var gameManager = NetworkGameManager.Instance;
        int slot = gameManager != null ? gameManager.GetRosterSlot(player) : -1;
        if (slot >= 0 && slot < _spawnPoints.Length && !_assignedSpawns.ContainsValue(slot))
            return slot;

        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            if (!_assignedSpawns.ContainsValue(i))
                return i;
        }

        // More players than spawn points: share one.
        return DeterministicRandom.Next(0, _spawnPoints.Length);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_UpdateList()
    {
        OnPlayersChangedEvent?.Invoke();
    }
}
