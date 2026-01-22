using Core;
using Fusion;
using System;
using UnityEngine;
using Zenject;

public abstract class BaseNetworkPlayerSpawner : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public event Action OnPlayersChangedEvent;

    [SerializeField] protected NetworkPrefabRef _playerPrefab;
    [SerializeField] protected Transform[] _spawnPoints;
    [Inject] protected DiContainer _container;

    [Networked, Capacity(6)]
    public NetworkDictionary<PlayerRef, Player> Players => default;

    public void RequestSpawn(Player player)
    {
        if (!HasStateAuthority) return;
        RespawnPlayer(player);
    }

    public virtual void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        Vector3 spawnPosition = GetSpawnPosition();
        NetworkObject playerObject = Runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
        var playerBehaviour = playerObject.GetComponent<Player>();

        _container.InjectGameObject(playerBehaviour.gameObject);

        Players.Add(player, playerBehaviour);
        Runner.SetPlayerObject(player, playerObject);
        RPC_UpdateList();
    }

    public virtual void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (Players.TryGet(player, out Player playerBehaviour))
        {
            Players.Remove(player);
            Runner.Despawn(playerBehaviour.Object);
            RPC_UpdateList();
        }
    }
    protected virtual void RespawnPlayer(Player player)
    {
        Vector3 spawnPosition = GetSpawnPosition();
        Quaternion spawnRotation = Quaternion.identity;

        if (player.TryGetComponent<NetworkCharacterController>(out var controller))
        {
            controller.Teleport(spawnPosition, spawnRotation);
            controller.Velocity = Vector3.zero;
        }
    }
    protected Vector3 GetSpawnPosition()
    {
        if (_spawnPoints.Length == 0) return Vector3.zero;
        return _spawnPoints[DeterministicRandom.Next(0, _spawnPoints.Length)].position;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_UpdateList()
    {
        OnPlayersChangedEvent?.Invoke();
    }
}
