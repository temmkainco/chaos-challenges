using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sky Rings: a plane race. Everyone flies a little toy plane (always moving; W/S nose up and down, A/D turn, Shift
/// boost) through the hoops in order - your next one glows gold - for two laps. Miss one and you have to turn back
/// for it. Hit the ground and you crash back at the last hoop you made. Finish order wins; at the bell the rest by
/// hoops. Layouts: Big Loop, Slalom, Tower. Twist: TAILWIND, everyone flies faster.
/// </summary>
public class SkyRingsMinigameController : RaceMinigameController
{
    private const int RINGS = 14, LAPS = 2;
    private const float RING_RADIUS = 3.2f;

    [SerializeField] private float _height = 14f;

    /// <summary>Per player slot: hoops flown through so far (the next one is this mod <see cref="RINGS"/>).</summary>
    [Networked, Capacity(32)] private NetworkArray<short> Passed { get; }

    public override VehicleKind Vehicle => VehicleKind.Plane;
    public override bool AllowShove => false;
    public override bool UsesDefaultControls => false;
    public override float MaxAltitude => Sky.y + 26f;
    public override string[] Variants => new[] { "Big Loop", "Slalom", "Tower" };
    public override string TwistTitle => "TAILWIND";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "Everyone flies faster for a while.";

    /// <summary>Planes can't use Ice Floor, Giants, Tiny or jump changes (they never touch the ground).</summary>
    public override bool SupportsChaos(ChaosModifier modifier) =>
        modifier is not (ChaosModifier.IceFloor or ChaosModifier.Giants or ChaosModifier.Tiny or ChaosModifier.LowGravity or ChaosModifier.SuperJump)
        && base.SupportsChaos(modifier);

    private readonly List<(Vector3 position, Vector3 normal)> _rings = new();
    private readonly List<Transform> _ringViews = new();
    private readonly Dictionary<PlayerRef, Vector3> _last = new();
    private int _shownNext = -1;

    private Vector3 Sky => _centre.position + Vector3.up * _height;
    private Vector3 Pad => Sky + Vector3.back * 34f;
    protected override float FallHeight => _centre.position.y - 4f;

    public override void Spawned()
    {
        base.Spawned();
        ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cube, Pad + Vector3.down * 0.5f, new Vector3(24f, 0.5f, 8f), ArenaProps.Purple), "BrickPlate");
        ArenaProps.Fence(transform, _centre.position, 62f, 60f, 56);
        for (int i = 0; i < RINGS; i++)
        {
            float t = i / (float)RINGS * Mathf.PI * 2f;
            Vector3 position = Variant switch
            {
                1 => Sky + new Vector3(Mathf.Sin(t) * 40f, 5f + Mathf.Sin(t * 3f) * 4f, -Mathf.Cos(t) * 22f + Mathf.Sin(t * 4f) * 6f),
                2 => Sky + new Vector3(Mathf.Sin(t) * 24f, 3f + i * 1.6f, -Mathf.Cos(t) * 24f),
                _ => Sky + new Vector3(Mathf.Sin(t) * 38f, 6f + Mathf.Sin(t * 2f) * 6f, -Mathf.Cos(t) * 30f),
            };
            _rings.Add((position, Vector3.forward));
        }
        for (int i = 0; i < RINGS; i++)
        {
            var normal = (_rings[(i + 1) % RINGS].position - _rings[(i + RINGS - 1) % RINGS].position).normalized;
            _rings[i] = (_rings[i].position, normal);
            // A hula hoop stood up across the course (the model lies flat: its up turns to face along the course).
            var view = ArenaModels.Spawn("Hoop", transform, _rings[i].position, Quaternion.FromToRotation(Vector3.up, normal),
                new Vector3(RING_RADIUS * 2.1f, 0.4f, RING_RADIUS * 2.1f));
            _ringViews.Add(view);
        }
        ArenaProps.Done();
    }

    protected override Vector3 StartPosition(int slot, int count, out float yaw)
    {
        yaw = Quaternion.LookRotation((_rings[0].position - Pad).WithY(0f)).eulerAngles.y;
        return Pad + new Vector3((slot - (Mathf.Max(1, count) - 1) * 0.5f) * 3.5f, 0.6f, 0f);
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        _last.Clear();
        for (int i = 0; i < 32; i++) Passed.Set(i, 0);
    }

    private int PassedBy(PlayerRef player) => Passed[player.PlayerId & 31];

    /// <summary>Hoops made, plus how close you are to the next one (so the scoreboard orders racers between hoops).</summary>
    protected override float Progress(PlayerRef player, Vector3 position)
    {
        int passed = PassedBy(player);
        var next = _rings[passed % RINGS].position;
        var previous = passed > 0 ? _rings[(passed - 1) % RINGS].position : Pad;
        float leg = Mathf.Max(1f, (next - previous).magnitude);
        return passed + Mathf.Clamp01(1f - (next - position).magnitude / leg) * 0.99f;
    }

    protected override bool IsFinished(float progress) => progress >= RINGS * LAPS;
    protected override string ProgressLabel(float progress) => $"Hoop {Mathf.Min(Mathf.FloorToInt(progress), RINGS * LAPS)}/{RINGS * LAPS}";

    protected override Vector3 RespawnPoint(PlayerRef player, float bestProgress, out float yaw)
    {
        int passed = PassedBy(player);
        var from = passed > 0 ? _rings[(passed - 1) % RINGS].position : Pad + Vector3.up * 0.6f;
        yaw = Quaternion.LookRotation((_rings[passed % RINGS].position - from).WithY(0f) + Vector3.forward * 0.001f).eulerAngles.y;
        return from;
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier != ChaosModifier.Twist) return;
        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null && entry.Value.TryGetComponent<PlayerMovement>(out var movement)) movement.Boost(8f);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || player.ControlsLocked) { if (player != null) _last.Remove(entry.Key); continue; }
            var now = player.transform.position + Vector3.up * 0.6f;
            if (_last.TryGetValue(entry.Key, out var before))
            {
                // Only the next hoop counts: it has to be flown through, the right way, inside the hoop.
                int slot = entry.Key.PlayerId & 31;
                var (centre, normal) = _rings[Passed[slot] % RINGS];
                float a = Vector3.Dot(before - centre, normal), b = Vector3.Dot(now - centre, normal);
                if (a < 0f && b >= 0f && (Vector3.Lerp(before, now, a / (a - b)) - centre).magnitude <= RING_RADIUS + 0.4f)
                {
                    Passed.Set(slot, (short)(Passed[slot] + 1));
                    RPC_Ring(centre, entry.Key);
                }
            }
            _last[entry.Key] = now;

            // Touching anything but the start pad is a crash.
            var pad = player.transform.position - Pad;
            bool onPad = Mathf.Abs(pad.x) < 12.5f && Mathf.Abs(pad.z) < 4.5f && pad.y > -1f;
            if (player.TryGetComponent<PlayerMovement>(out var movement) && movement.IsGrounded && !onPad)
            {
                _last.Remove(entry.Key);
                RPC_Crash(player.transform.position);
                HandlePlayerFell(player);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Ring(Vector3 position, PlayerRef player)
    {
        if (player == Runner.LocalPlayer) Sfx.Play(Sound.Safe, 1f, 0.1f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Crash(Vector3 position)
    {
        Sfx.Play(Sound.Boom, 0.8f, 0.1f);
        LowPolyExplosion.Play(position + Vector3.up, 2f, Resources.Load<Material>("ThrowArc"));
    }

    /// <summary>Your next hoop glows gold and big; the rest stay plain.</summary>
    public override void Render()
    {
        base.Render();
        if (!Runner.LocalPlayer.IsRealPlayer || _ringViews.Count == 0) return;
        int next = PassedBy(Runner.LocalPlayer) % RINGS;
        if (next == _shownNext) return;
        _shownNext = next;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < _ringViews.Count; i++)
        {
            if (_ringViews[i] == null) continue;
            bool target = i == next;
            foreach (var renderer in _ringViews[i].GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", target ? new Color(1f, 0.8f, 0.2f) : new Color(0.75f, 0.8f, 0.85f));
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
