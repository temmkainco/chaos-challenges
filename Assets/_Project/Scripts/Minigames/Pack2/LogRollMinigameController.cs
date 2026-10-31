using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Log Roll (after Fall Guys' Roll Out): giant logs spin under your feet high above the studio. The spin carries you
/// toward the edge, so keep walking against it, and jump the ridges as they roll over the top. The spin speeds up and
/// changes direction. Last one standing wins. Layouts: Big Log, Twin Logs (spinning opposite ways), Speed Log
/// (thinner and faster). Twist: REVERSE, the logs flip direction and spin faster.
/// </summary>
public class LogRollMinigameController : EliminationMinigameController
{
    private const int RIDGES = 3;

    [SerializeField] private Transform _centre;
    [SerializeField] private float _height = 7f;

    /// <summary>The first log's roll (degrees; rate in degrees per second, sign = direction), evaluated per tick and per frame.</summary>
    [Networked] private NetMotion Roll { get; set; }
    /// <summary>Spin speed in radians per second (sign = direction).</summary>
    private float Spin => Roll.Rate * Mathf.Deg2Rad;
    private void SetSpin(float radians) => Roll = Roll.Retarget(Runner, radians * Mathf.Rad2Deg, 360f);
    [Networked] private TickTimer NextFlip { get; set; }

    public override string[] Variants => new[] { "Big Log", "Twin Logs", "Speed Log" };
    public override string TwistTitle => "REVERSE";
    public override string TwistDescription => "The logs flip direction and spin faster.";
    public override bool AllowsSprint => false;

    private readonly List<Transform> _logViews = new();
    private readonly Dictionary<PlayerRef, int> _hitTick = new();

    private int LogCount => Variant == 1 ? 2 : 1;
    private float Radius => Variant == 0 ? 3.6f : Variant == 1 ? 3f : 2.6f;
    private const float LENGTH = 22f;
    private Vector3 Axis(int log) => _centre.position + Vector3.up * _height + Vector3.forward * (LogCount == 1 ? 0f : log == 0 ? -4.1f : 4.1f);
    private float SpinOf(int log) => log == 0 ? Spin : -Spin;
    protected override float OutHeight => _centre.position.y + _height - Radius - 3f;

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        int log = slot % LogCount;
        float x = Mathf.Lerp(-LENGTH * 0.35f, LENGTH * 0.35f, count <= 1 ? 0.5f : slot / (float)(count - 1));
        return Axis(log) + Vector3.up * (Radius + 0.5f) + Vector3.right * x;
    }

    public override void Spawned()
    {
        base.Spawned();
        // Start with no ridge on top: everyone spawns up there.
        if (HasStateAuthority) Roll = NetMotion.Still(Runner, 60f);
        for (int log = 0; log < LogCount; log++)
        {
            // Static collider (a cylinder looks the same at any angle); the visual log turns. The mesh comes from a
            // cylinder primitive: Unity's built-in "Cylinder.fbx" resource is missing in builds, which left the log
            // without a collider there and everyone fell through it.
            var collider = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            collider.name = "LogCollider";
            Destroy(collider.GetComponent<Collider>());
            collider.GetComponent<Renderer>().enabled = false;
            collider.transform.SetParent(transform, false);
            collider.transform.SetPositionAndRotation(Axis(log), Quaternion.Euler(0f, 0f, 90f));
            collider.transform.localScale = new Vector3(Radius * 2f, LENGTH * 0.5f, Radius * 2f);
            var mesh = collider.AddComponent<MeshCollider>();
            mesh.sharedMesh = collider.GetComponent<MeshFilter>().sharedMesh;
            mesh.convex = true;

            var view = new GameObject("Log").transform;
            view.SetParent(transform, false);
            view.position = Axis(log);
            // A wood log (its long side along X, like the collider) with three thinner logs along it as ridges.
            ArenaModels.Spawn("Log", view, Axis(log), Quaternion.identity, new Vector3(LENGTH, Radius * 2f, Radius * 2f));
            for (int r = 0; r < RIDGES; r++)
            {
                var spin = Quaternion.Euler(r * 360f / RIDGES, 0f, 0f);
                ArenaModels.Spawn("Log", view, Axis(log) + spin * (Vector3.up * (Radius + 0.2f)), spin, new Vector3(LENGTH * 0.96f, 0.6f, 0.6f));
            }
            _logViews.Add(view);
        }
        ArenaProps.Done();
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        // A gentle start (standing still on a fast log threw people off in two seconds), building up over the round.
        SetSpin(0.15f * (Variant == 2 ? 1.3f : 1f));
        NextFlip = TickTimer.CreateFromSeconds(Runner, 12f);
        _hitTick.Clear();
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier == ChaosModifier.Twist) SetSpin(-Spin * 1.4f);
    }

    protected override void OnChaosEnded(ChaosModifier modifier)
    {
        if (modifier == ChaosModifier.Twist) SetSpin(Spin / 1.4f);
    }

    /// <summary>The spinning surface's speed where you stand (horizontal only): this is what carries you.</summary>
    public override Vector3 GroundDrift(Vector3 position)
    {
        if (!IsGameActive) return Vector3.zero;
        for (int log = 0; log < LogCount; log++)
        {
            var rel = position - Axis(log);
            rel.x = 0f;
            if (rel.magnitude > Radius + 0.6f || Mathf.Abs(position.x - Axis(log).x) > LENGTH * 0.5f) continue;
            var surface = Vector3.Cross(Vector3.right * SpinOf(log), rel);
            return new Vector3(0f, 0f, surface.z);
        }
        return Vector3.zero;
    }

    protected override bool SimulatedOnClients => true;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!IsGameActive) return;
        float dt = Runner.DeltaTime;
        if (HasStateAuthority)
        {
            float elapsed = Definition.GameDuration * MatchSettings.LengthMultiplier - TimeRemaining;
            float target = Mathf.Lerp(0.15f, 0.85f, Mathf.Clamp01(elapsed / 60f)) * (Variant == 2 ? 1.3f : 1f) * (TwistActive ? 1.4f : 1f);
            // Speeding up a little at a time: re-anchored every half second rather than every tick.
            float spin = Mathf.Sign(Spin) * Mathf.MoveTowards(Mathf.Abs(Spin), target, 0.2f * dt * Runner.TickRate / 2f);
            if (Runner.Tick % (Runner.TickRate / 2) == 0 && !Mathf.Approximately(spin, Spin)) SetSpin(spin);
            if (NextFlip.Expired(Runner) && !TwistActive)
            {
                NextFlip = TickTimer.CreateFromSeconds(Runner, Random.Range(9f, 14f));
                SetSpin(-Spin);
                Announce("SWITCH!");
            }
        }
        float roll = Roll.AtTick(Runner);
        float previous = roll - Roll.Rate * dt;

        // Ridges rolling over the top knock off anyone standing low in their way.
        foreach (var who in HitTestedContestants(_alive))
        {
            if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) continue;
            if (CoolingDown(_hitTick, who, Runner.TickRate / 2)) continue;
            for (int log = 0; log < LogCount; log++)
            {
                var rel = player.transform.position - Axis(log);
                float distance = new Vector2(rel.y, rel.z).magnitude;
                // Only feet down on the log, below the ridge's top (0.5 m), can be caught; anyone in the air is clear
                // (the host sees a client's jump a moment late).
                if (distance > Radius + 0.35f || Mathf.Abs(rel.x) > LENGTH * 0.5f) continue;
                if (!player.TryGetComponent<PlayerMovement>(out var movement) || !movement.IsGrounded) continue;
                // Angle round the log's axis (0 = straight up, growing toward +Z like the log's own rotation).
                float at = Mathf.Atan2(rel.z, rel.y) * Mathf.Rad2Deg;
                float sign = log == 0 ? 1f : -1f;
                float from = previous * sign, to = roll * sign;
                bool hit = false;
                for (int r = 0; r < RIDGES; r++)
                {
                    float offset = r * 360f / RIDGES;
                    float travel = Mathf.DeltaAngle(from + offset, to + offset);
                    float start = Mathf.DeltaAngle(from + offset, at);
                    // The ridge's half-width (0.3 m) plus a little of the body, as an angle at this log's radius.
                    float width = 0.5f / Radius * Mathf.Rad2Deg;
                    if (travel >= 0f ? start >= -width && start <= travel + width : start <= width && start >= travel - width) hit = true;
                }
                if (!hit) continue;
                _hitTick[who] = Runner.Tick;
                movement.Launch(new Vector3(0f, 6f, Mathf.Sign(SpinOf(log)) * 6f));
                break;
            }
        }
    }

    public override void Render()
    {
        base.Render();
        float angle = Roll.Render(Runner);
        for (int log = 0; log < _logViews.Count; log++)
            _logViews[log].localRotation = Quaternion.Euler(log == 0 ? angle : -angle, 0f, 0f);
    }
}
