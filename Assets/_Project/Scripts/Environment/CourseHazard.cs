using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A moving obstacle (pendulum hammer or spinning sweeper bar). Every peer poses it from the network clock, so it
/// looks the same everywhere without replication; only the host tests the striking part against contestants and
/// knocks them away along the hazard's motion with <see cref="PlayerMovement.Launch"/>, which replicates.
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
    private readonly Collider[] _hits = new Collider[12];
    private readonly Dictionary<Player, float> _cooldown = new();
    private Vector3 _lastStrikerPos;

    public void Configure(Kind kind, Vector3 axis, float amount, float period, float phase, Transform striker, float hitRadius, float hitLength)
    {
        _kind = kind; _axis = axis; _amount = amount; _period = period; _phase = phase;
        _striker = striker; _hitRadius = hitRadius; _hitLength = hitLength;
    }

    private void Awake() => _rest = transform.localRotation;

    private static float Clock()
    {
        var mc = MinigameController.Current;
        if (mc != null && mc.Runner != null && mc.Runner.IsRunning)
            return mc.Runner.IsServer ? mc.Runner.SimulationTime : mc.Runner.LocalRenderTime;
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

    private void FixedUpdate()
    {
        var mc = MinigameController.Current;
        if (_striker == null || mc == null || mc.Object == null || !mc.Object.IsValid || !mc.HasStateAuthority || !mc.IsGameActive)
            return;

        Pose(Clock());
        Vector3 pos = _striker.position;
        Vector3 motion = pos - _lastStrikerPos;
        _lastStrikerPos = pos;

        int count;
        if (_hitLength > 0f)
        {
            var half = _striker.up * (_hitLength * 0.5f);
            count = Physics.OverlapCapsuleNonAlloc(pos - half, pos + half, _hitRadius, _hits, ~0, QueryTriggerInteraction.Ignore);
        }
        else
            count = Physics.OverlapSphereNonAlloc(pos, _hitRadius, _hits, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var player = _hits[i].GetComponentInParent<Player>();
            if (player == null || player.Object == null || player.ControlsLocked) continue;
            if (mc is TilefallMinigameController tilefall && tilefall.IsProtected(player.Object.InputAuthority)) continue;   // just respawned
            if (_cooldown.TryGetValue(player, out var until) && Time.time < until) continue;
            // Low sweeper bars are jumped: anyone in the air or above the bar is clear. (The host sees a client's
            // jump a moment late, and the old full-radius test hit players who had cleared it on their screen.)
            if (_kind == Kind.Spinner && player.TryGetComponent<PlayerMovement>(out var mover)
                && (!mover.IsGrounded || player.transform.position.y > pos.y - 0.25f)) continue;
            _cooldown[player] = Time.time + 1f;

            // Push along the striker's motion; fall back to "away from the striker" when it barely moves.
            var push = new Vector3(motion.x, 0f, motion.z);
            if (push.sqrMagnitude < 0.0001f)
                push = new Vector3(player.transform.position.x - pos.x, 0f, player.transform.position.z - pos.z);
            if (push.sqrMagnitude < 0.0001f) push = Vector3.right;
            if (player.TryGetComponent<PlayerMovement>(out var movement))
                movement.Launch(push.normalized * _knockback + Vector3.up * _knockUp);
            Debug.Log($"[Hazard] {name} hit {player.Object.InputAuthority}");
            HazardHitFx(player.transform.position);
        }
    }

    private static void HazardHitFx(Vector3 position)
    {
        Sfx.Play(Sound.Boom, 0.6f, 0.2f);
        WorldPopup.Show(position + Vector3.up * 2.4f, "BONK!", new Color(1f, 0.79f, 0.24f), 7f);
    }
}
