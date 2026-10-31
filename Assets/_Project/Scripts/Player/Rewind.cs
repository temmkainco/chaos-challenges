using UnityEngine;

/// <summary>
/// Host-side lag compensation for contact actions (grabbing a tail, handing over the bomb, shoving). A client sees the
/// other contestants a moment in the past, so "right next to me" on its screen could be a few metres off on the host,
/// and grabs that looked certain failed. With every input a client reports the tick it was showing the others at
/// (<see cref="NetworkInputData.ViewTick"/>); the host keeps each contestant's recent positions and checks the action
/// against where the target stood on the actor's screen, rewinding at most <see cref="MAX_SECONDS"/>.
/// </summary>
public static class Rewind
{
    /// <summary>Furthest the host looks back (a fair limit for players on bad connections).</summary>
    public const float MAX_SECONDS = 0.25f;

    /// <summary>Host. Where <paramref name="target"/> stood on <paramref name="actor"/>'s screen when the actor acted.</summary>
    public static Vector3 PositionSeenBy(Player actor, Player target)
    {
        var now = target.transform.position;
        if (actor == null || target == null || actor.Object == null || target.Object == null || actor.Object.HasInputAuthority)
            return now;   // the host's own contestant sees the present
        if (!actor.TryGetComponent<PlayerMovement>(out var actorMovement) || !target.TryGetComponent<PlayerMovement>(out var targetMovement))
            return now;
        var runner = target.Runner;
        int viewTick = actorMovement.ViewTick;
        if (viewTick <= 0) return now;
        int tick = Mathf.Clamp(viewTick, runner.Tick - Mathf.CeilToInt(MAX_SECONDS * runner.TickRate), runner.Tick);
        return targetMovement.PositionAt(tick);
    }
}
