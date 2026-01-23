using UnityEngine;
using Zenject;

public class BaseRespawnZone : MonoBehaviour
{
    [Inject] private BasePlayerSpawner _spawner;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Player>(out Player player))
        {
            _spawner.RequestSpawn(player);
        }
    }
}
