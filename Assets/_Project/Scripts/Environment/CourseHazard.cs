using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A moving obstacle (pendulum hammer or spinning sweeper bar). Every peer poses it from the network clock, so it
/// looks the same everywhere without replication: drawn at the local render time (in step with your own predicted
/// player) and tested at the tick being simulated. In each contestant's own network tick (<see cref="TestAll"/>, via
/// <see cref="MinigameController.SimulateContestant"/>) the striking part is tested against it, on the host for every
/// contestant and on a client for its own predicted one, and a hit knocks it away along the hazard's motion with
/// <see cref="PlayerMovement.Launch"/>: the host's knockback replicates, the client's lands on its own screen at once.
/// </summary>
public class CourseHazard : MonoBehaviour
{
    public enum Kind { Pendulum, Spinner }

    [SerializeField] private Kind _kind = Kind.Pendulum;
    [SerializeField, Tooltip("Local axis the pivot rotates around")] private Vector3 _axis = Vector3.forward;
    [SerializeField, Tooltip("Pendulum: max angle; spinner: degrees per second")] private float _amount = 65f;
    [SerializeField, Tooltip("Pendulum period in seconds")] private float _period = 3f;
    [SerializeField] private float _phase;
    [SerializeField, Tooltip("The part that hits (hammer head / bar)")] private Transform _striker;
    [SerializeField] private float _hitRadius = 1.3f;
    [SerializeField, Tooltip("Spinner bars hit along their length")] private float _hitLength;
    [SerializeField] private float _knockback = 13f;
    [SerializeField] private float _knockUp = 6f;

    private Quaternion _rest;
    private readonly Dictionary<Player, int> _cooldownUntilTick = new();

    private static readonly List<CourseHazard> Active = new();

    public void Configure(Kind kind, Vector3 axis, float amount, float period, float phase, Transform striker, float hitRadius, float hitLength)
    {
        _kind = kind; _axis = axis; _amount = amount; _period = period; _phase = phase;
        _striker = striker; _hitRadius = hitRadius; _hitLength = hitLength;
    }

    private void Awake() => _rest = transform.localRotation;
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    private static float Clock()
    {
        var mc = MinigameController.Current;
        if (mc != null && mc.Runner != null && mc.Runner.IsRunning)
            return mc.Runner.LocalRenderTime;
        return Time.time;
    }

    private void Pose(float t)
    {
        float angle = _kind == Kind.Pendulum
            ? Mathf.Sin((t / _period + _phase) * Mathf.PI * 2f) * _amount
            : (t * _amount + _phase * 360f) % 360f;
        transform.localRotation = _rest * Quaternion.AngleAxis(angle, _axis);
    }

    private void Update() => Pose(Clock());

    /// <summary>
    /// From a contestant's own network tick, before it moves (the host for everyone, a client for its own): every hazard
    /// is posed at the tick and tested against that contestant.
    /// </summary>
    public static void TestAll(MinigameController mc, Player player, PlayerMovement movement)
    {
        var runner = mc.Runner;
        float now = runner.Tick * runner.DeltaTime;
        foreach (var hazard in Active)
            if (hazard._striker != null)
                hazard.Test(mc, player, movement, now, runner.DeltaTime, runner.Tick, runner.TickRate);
    }

    private void Test(MinigameController mc, Player player, PlayerMovement movement, float now, float dt, int tick, int tickRate)
    {
        // Cooling down after a hit, but re-running the hit's own tick (client re-simulation) applies it again.
        if (_cooldownUntilTick.TryGetValue(player, out int until) && tick < until && tick > until - tickRate) return;

        // Where the striker was a tick ago and is now: its motion gives the push direction.
        Pose(now - dt);
        Vector3 before = _striker.position;
        Pose(now);
        Vector3 pos = _striker.position;
        Vector3 motion = pos - before;

        // Inside the striking part: a capsule along a sweeper bar, a sphere round a hammer head.
        var body = player.transform.position + Vector3.up;
        Vector3 closest = pos;
        if (_hitLength > 0f)
        {
            var half = _striker.up * (_hitLength * 0.5f);
            var a = pos - half;
            var ab = half * 2f;
            closest = a + ab * Mathf.Clamp01(Vector3.Dot(body - a, ab) / ab.sqrMagnitude);
        }
        if ((body - closest).magnitude > _hitRadius + 0.5f) return;
        // Low sweeper bars are jumped: anyone in the air or above the bar is clear.
        if (_kind == Kind.Spinner && (!movement.IsGrounded || player.transform.position.y > pos.y - 0.25f)) return;
        _cooldownUntilTick[player] = tick + tickRate;

        // Push along the striker's motion; fall back to "away from the striker" when it barely moves.
        var push = new Vector3(motion.x, 0f, motion.z);
        if (push.sqrMagnitude < 0.0001f)
            push = new Vector3(player.transform.position.x - pos.x, 0f, player.transform.position.z - pos.z);
        if (push.sqrMagnitude < 0.0001f) push = Vector3.right;
        movement.Launch(push.normalized * _knockback + Vector3.up * _knockUp);
        if (mc.Runner.IsResimulation) return;
        if (mc.HasStateAuthority) Debug.Log($"[Hazard] {name} hit {player.Object.InputAuthority}");
        HazardHitFx(player.transform.position);
    }

    private static void HazardHitFx(Vector3 position)
    {
        Sfx.Play(Sound.Boom, 0.6f, 0.2f);
        WorldPopup.Show(position + Vector3.up * 2.4f, "BONK!", new Color(1f, 0.79f, 0.24f), 7f);
    }
}
