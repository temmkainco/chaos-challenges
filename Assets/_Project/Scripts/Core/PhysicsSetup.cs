using UnityEngine;

/// <summary>
/// Clients predict their own contestant with CharacterController.Move but never step the physics scene themselves,
/// so colliders built or moved from code (raised arenas, shrinking ring, Tilefall tiles and hammers, the lounge)
/// were never pushed to the physics engine there: clients ran "inside" floors that looked solid and got snapped back
/// by the host (the playtest's sinking and teleporting). Syncing transforms before every physics query fixes it.
/// </summary>
public static class PhysicsSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#pragma warning disable CS0618
        Physics.autoSyncTransforms = true;
#pragma warning restore CS0618
        // Contestants don't block each other: a client sees the others a moment in the past, so hard contacts came out
        // differently on the host and were corrected (PlayerMovement eases overlapping contestants apart instead).
        Physics.IgnoreLayerCollision(ArenaProps.PLAYER_LAYER, ArenaProps.PLAYER_LAYER, true);
        // Arena fences stop contestants only: bombs, prizes and everything else pass through.
        for (int layer = 0; layer < 32; layer++)
            Physics.IgnoreLayerCollision(ArenaProps.FENCE_LAYER, layer, layer != ArenaProps.PLAYER_LAYER);
    }
}
