using Fusion;
using UnityEngine;
using Zenject;

public class LobbyPlayerRespawnZone : MonoBehaviour
{
    [Inject] private NetworkPlayerSpawner _spawner;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Player>(out Player player))
        {
            Debug.Log($"Player {player.Nickname} entered respawn zone. Respawning...");
            _spawner.RequestSpawn(player);
        }
    }
}
