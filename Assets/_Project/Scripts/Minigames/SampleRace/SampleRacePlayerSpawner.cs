using Core;
using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class SampleRacePlayerSpawner : BasePlayerSpawner
{
    private readonly List<int> _freeSpawnIndices = new();
    private readonly Dictionary<PlayerRef, int> _assignedSpawns = new();

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        InitializeSpawnPoints();
        base.Spawned();
    }

    private void InitializeSpawnPoints()
    {
        _freeSpawnIndices.Clear();

        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            _freeSpawnIndices.Add(i);
        }
    }


    public override void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (Players.ContainsKey(player))
        {
            Debug.LogWarning($"Player {player} already spawned, skipping");
            return;
        }

        if (_freeSpawnIndices.Count == 0)
        {
            Debug.LogError("No free spawn points left!");
            return;
        }

        int spawnIndex = _freeSpawnIndices[0];
        _freeSpawnIndices.RemoveAt(0);
        _assignedSpawns[player] = spawnIndex;

        Vector3 spawnPosition = _spawnPoints[spawnIndex].position;

        NetworkObject playerObject = Runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
        var playerBehaviour = playerObject.GetComponent<Player>();
        playerBehaviour.Input.Controls.Disable();
        _container.InjectGameObject(playerBehaviour.gameObject);

        Players.Add(player, playerBehaviour);
        Runner.SetPlayerObject(player, playerObject);

        RPC_UpdateList();
    }

    public override void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (_assignedSpawns.TryGetValue(player, out int spawnIndex))
        {
            _assignedSpawns.Remove(player);
            _freeSpawnIndices.Add(spawnIndex);
        }

        base.PlayerLeft(player);
    }
}
