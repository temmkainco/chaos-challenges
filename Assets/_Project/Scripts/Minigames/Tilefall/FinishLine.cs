using Fusion;
using UnityEngine;
using Zenject;

/// <summary>Trigger at the end of the Tilefall course. Checked on the host only.</summary>
public class FinishLine : NetworkBehaviour
{
    [InjectOptional] private TilefallMinigameController _controller;

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null || !HasStateAuthority) return;
        // Players being despawned (scene change) can still touch the trigger for one physics step.
        if (!other.TryGetComponent<Player>(out var player) || player.Object == null || !player.Object.IsValid) return;

        // Scene network objects can be spawned before the scene context injects them.
        if (_controller == null)
            _controller = FindFirstObjectByType<TilefallMinigameController>();

        if (_controller != null)
            _controller.PlayerFinished(player.Object.InputAuthority);
    }
}
