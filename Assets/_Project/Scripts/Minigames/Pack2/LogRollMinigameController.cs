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

    /// <summary>Log rotation (degrees) and spin speed (radians per second, sign = direction) for the first log.</summary>
    [Networked] private float Angle { get; set; }
    [Networked] private float Spin { get; set; }
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
        for (int log = 0; log < LogCount; log++)
        {
            // Static collider (a cylinder looks the same at any angle); the visual log turns.
            var collider = new GameObject("LogCollider", typeof(MeshCollider));
            collider.transform.SetParent(transform, false);
            collider.transform.SetPositionAndRotation(Axis(log), Quaternion.Euler(0f, 0f, 90f));
            collider.transform.localScale = new Vector3(Radius * 2f, LENGTH * 0.5f, Radius * 2f);
            var mesh = collider.GetComponent<MeshCollider>();
            mesh.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            mesh.convex = true;

            var view = new GameObject("Log").transform;
            view.SetParent(transform, false);
            view.position = Axis(log);
            var trunk = ArenaProps.Block(view, PrimitiveType.Cylinder, Axis(log), new Vector3(Radius * 2f, LENGTH, Radius * 2f), new Color(0.62f, 0.4f, 0.24f));
            Destroy(trunk.GetComponent<Collider>());
            trunk.localPosition = Vector3.zero;
            trunk.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int r = 0; r < RIDGES; r++)
            {
                float a = r * 360f / RIDGES;
                var ridge = ArenaProps.Block(view, PrimitiveType.Cube, Axis(log), new Vector3(LENGTH * 0.96f, 0.45f, 0.55f), r == 0 ? ArenaProps.Pink : ArenaProps.Yellow);
                Destroy(ridge.GetComponent<Collider>());
                ridge.localRotation = Quaternion.Euler(a, 0f, 0f);
                ridge.localPosition = ridge.localRotation * (Vector3.up * (Radius + 0.15f));
            }
            // Bark stripes so the spin is easy to see.
            for (int s = 0; s < 8; s++)
            {
                var stripe = ArenaProps.Block(view, PrimitiveType.Cube, Axis(log), new Vector3(LENGTH * 0.98f, 0.06f, 0.5f), new Color(0.45f, 0.27f, 0.15f));
                Destroy(stripe.GetComponent<Collider>());
                stripe.localRotation = Quaternion.Euler(s * 45f + 22.5f, 0f, 0f);
                stripe.localPosition = stripe.localRotation * (Vector3.up * (Radius + 0.01f));
            }
            _logViews.Add(view);
            // End walls keep you from walking off the ends.
            foreach (float end in new[] { -1f, 1f })
                ArenaProps.Block(transform, PrimitiveType.Cube, Axis(log) + Vector3.right * end * (LENGTH * 0.5f + 0.3f) + Vector3.down * (Radius + 0.5f),
                    new Vector3(0.6f, Radius * 2f + 2.5f, Radius * 2f + 0.6f), ArenaProps.Blue);
        }
        ArenaProps.Done();
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        Spin = 0.35f * (Variant == 2 ? 1.4f : 1f);
        NextFlip = TickTimer.CreateFromSeconds(Runner, 12f);
        _hitTick.Clear();
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier == ChaosModifier.Twist) Spin = -Spin * 1.4f;
    }

    protected override void OnChaosEnded(ChaosModifier modifier)
    {
        if (modifier == ChaosModifier.Twist) Spin /= 1.4f;
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

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        float dt = Runner.DeltaTime;
        float elapsed = Definition.GameDuration * MatchSettings.LengthMultiplier - TimeRemaining;
        float target = Mathf.Lerp(0.35f, 0.95f, Mathf.Clamp01(elapsed / 60f)) * (Variant == 2 ? 1.4f : 1f) * (TwistActive ? 1.4f : 1f);
        Spin = Mathf.Sign(Spin) * Mathf.MoveTowards(Mathf.Abs(Spin), target, 0.2f * dt);
        if (NextFlip.Expired(Runner) && !TwistActive)
        {
            NextFlip = TickTimer.CreateFromSeconds(Runner, Random.Range(9f, 14f));
            Spin = -Spin;
            Announce("SWITCH!");
        }
        float previous = Angle;
        Angle += Spin * Mathf.Rad2Deg * dt;

        // Ridges rolling over the top knock off anyone standing low in their way.
        foreach (var who in _alive)
        {
            if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) continue;
            if (_hitTick.TryGetValue(who, out int last) && Runner.Tick - last < Runner.TickRate / 2) continue;
            for (int log = 0; log < LogCount; log++)
            {
                var rel = player.transform.position - Axis(log);
                float distance = new Vector2(rel.y, rel.z).magnitude;
                if (distance > Radius + 0.55f || Mathf.Abs(rel.x) > LENGTH * 0.5f) continue;
                // Angle round the log's axis (0 = straight up, growing toward +Z like the log's own rotation).
                float at = Mathf.Atan2(rel.z, rel.y) * Mathf.Rad2Deg;
                float sign = log == 0 ? 1f : -1f;
                float from = previous * sign, to = Angle * sign;
                bool hit = false;
                for (int r = 0; r < RIDGES; r++)
                {
                    float offset = r * 360f / RIDGES;
                    float travel = Mathf.DeltaAngle(from + offset, to + offset);
                    float start = Mathf.DeltaAngle(from + offset, at);
                    float width = 10f;
                    if (travel >= 0f ? start >= -width && start <= travel + width : start <= width && start >= travel - width) hit = true;
                }
                if (!hit || !player.TryGetComponent<PlayerMovement>(out var movement)) continue;
                _hitTick[who] = Runner.Tick;
                movement.Launch(new Vector3(0f, 5f, Mathf.Sign(SpinOf(log)) * 4f));
                break;
            }
        }
    }

    public override void Render()
    {
        base.Render();
        for (int log = 0; log < _logViews.Count; log++)
            _logViews[log].localRotation = Quaternion.Euler(log == 0 ? Angle : -Angle, 0f, 0f);
    }
}
