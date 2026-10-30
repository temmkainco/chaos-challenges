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

    /// <summary>Spread match contestants evenly over the spawn points.</summary>
    protected virtual bool SpreadSpawns => false;

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

    /// <summary>Metres below the lowest spawn point that count as "fell off the world".</summary>
    private const float FALL_LIMIT = 12f;

    /// <summary>
    /// Host only: a fall guard for every scene (the lobby had none). Whoever drops far below the spawn points is handed
    /// to the minigame first (it may knock them out or send them to the lounge); otherwise they respawn.
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || Runner.Tick % 15 != 0 || _spawnPoints == null || _spawnPoints.Length == 0)
            return;

        float lowest = float.MaxValue;
        foreach (var point in _spawnPoints)
            if (point != null) lowest = Mathf.Min(lowest, point.position.y);
        float limit = lowest - FALL_LIMIT;

        var minigame = MinigameController.Current;
        bool minigameReady = minigame != null && minigame.Object != null && minigame.Object.IsValid;
        foreach (var entry in Players)
        {
            var player = entry.Value;
            if (player == null || player.transform.position.y > limit) continue;
            if (minigameReady && minigame.HandlePlayerFell(player)) continue;
            RequestSpawn(player);
        }
    }

    /// <summary>Height of the lowest spawn point (the floor of the arena the round starts on).</summary>
    public float LowestSpawnY
    {
        get
        {
            float lowest = float.MaxValue;
            if (_spawnPoints != null)
                foreach (var point in _spawnPoints)
                    if (point != null) lowest = Mathf.Min(lowest, point.position.y);
            return lowest == float.MaxValue ? 0f : lowest;
        }
    }

    /// <summary>Host only. Puts the player back on their spawn point (respawn zones).</summary>
    public void RequestSpawn(Player player)
    {
        if (!HasStateAuthority) return;

        var spawn = GetSpawnPoint(player.Object.InputAuthority);
        Teleport(player, spawn.position, spawn.rotation);
    }

    /// <summary>Host only.</summary>
    public void Teleport(Player player, Vector3 position, Quaternion? rotation = null)
    {
        if (!HasStateAuthority || player == null) return;

        if (player.TryGetComponent<NetworkCharacterController>(out var controller))
        {
            controller.Teleport(position, rotation ?? player.transform.rotation);
            controller.Velocity = Vector3.zero;
        }
    }

    /// <summary>Host only. The player's own spawn point.</summary>
    public Vector3 GetSpawnPosition(PlayerRef player) => GetSpawnPoint(player).position;

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
        NetworkObject playerObject = Runner.Spawn(_playerPrefab, spawn.position, spawn.rotation, player);
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
        // During a match, spread the contestants evenly over the points (2 players on a ring of 6 face each other,
        // 3 players in Tilefall get every other lane) instead of filling neighbouring points.
        var gameManager = NetworkGameManager.Instance;
        int slot = gameManager != null ? gameManager.GetRosterSlot(player) : -1;
        int contestants = gameManager != null ? gameManager.RosterCount : 0;
        if (slot >= 0 && contestants > 0)
        {
            int spread = SpreadSpawns ? slot * _spawnPoints.Length / Mathf.Max(contestants, 1) : slot;
            if (spread < _spawnPoints.Length && !_assignedSpawns.ContainsValue(spread))
                return spread;
        }

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
