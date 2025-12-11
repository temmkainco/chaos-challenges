using Core;
using Cysharp.Threading.Tasks;
using Fusion;
using Platform;
using System;
using UnityEngine;
using Zenject;

public class NetworkPlayerSpawner : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public event Action OnPlayersChangedEvent;

    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private Transform[] _spawnPoints;
    [Inject] private DiContainer _container;

    [Networked, Capacity(6)]
    public NetworkDictionary<PlayerRef, Player> Players => default;

    public void RequestSpawn(Player player)
    {
        if(!HasStateAuthority)
            return;

        RespawnPlayer(player);
    }

    private void RespawnPlayer(Player player)
    {
        Vector3 spawnPosition = _spawnPoints[DeterministicRandom.Next(0, _spawnPoints.Length)].position;
        Quaternion spawnRotation = Quaternion.identity;

        if (player.TryGetComponent<NetworkCharacterController>(out var controller))
        {
            controller.Teleport(spawnPosition, spawnRotation);
            controller.Velocity = Vector3.zero;
        }
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        Vector3 spawnPosition = _spawnPoints[DeterministicRandom.Next(0, _spawnPoints.Length)].position;
        NetworkObject playerObject = Runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
        var playerBehaviour = playerObject.GetComponent<Player>();

        _container.InjectGameObject(playerBehaviour.gameObject);

        Players.Add(player, playerBehaviour);
        RPC_UpdateList();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateList()
    {
        OnPlayersChangedEvent?.Invoke();
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        if (Players.TryGet(player, out Player playerBehaviour))
        {
            Players.Remove(player);
            Runner.Despawn(playerBehaviour.Object);
            RPC_UpdateList();
        }
    }
}