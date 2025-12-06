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
    [Inject] private DiContainer _container;

    [Networked, Capacity(8)]
    public NetworkDictionary<PlayerRef, Player> Players => default;

    public void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        Vector3 spawnPosition = new Vector3((player.RawEncoded % 8) * 2, 1, 0);
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