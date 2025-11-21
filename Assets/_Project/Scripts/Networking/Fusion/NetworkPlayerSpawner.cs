using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class NetworkPlayerSpawner : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [Networked, Capacity(8)] private NetworkDictionary<PlayerRef, Player> Players => default;

    public void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        Vector3 spawnPosition = new Vector3((player.RawEncoded % 8) * 2, 1, 0);
        NetworkObject playerObject = Runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);

        Players.Add(player, playerObject.GetComponent<Player>());
    }

    public void PlayerLeft(PlayerRef player)
    {
        if(!HasStateAuthority)
            return;

        if(Players.TryGet(player, out Player playerBehaviour))
        {
            Players.Remove(player);
            Runner.Despawn(playerBehaviour.Object);
        }
    }
}
