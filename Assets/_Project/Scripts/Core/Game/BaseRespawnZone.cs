using UnityEngine;
using Zenject;

/// <summary>Kill zone under an arena. The minigame decides what a fall means; by default the player goes back to their spawn point.</summary>
public class BaseRespawnZone : MonoBehaviour
{
    [Inject] private BasePlayerSpawner _spawner;
    [InjectOptional] private MinigameController _minigame;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<Player>(out Player player))
            return;

        if (_minigame != null && _minigame.HandlePlayerFell(player))
            return;

        _spawner.RequestSpawn(player);
    }
}
