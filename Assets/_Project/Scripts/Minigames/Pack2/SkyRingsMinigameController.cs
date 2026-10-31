using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sky Rings: everyone flies a little plane (always moving; W/S nose up and down, A/D turn, Shift boost). Fly through
/// rings for a point each, gold ones for three; a ring can't score for you again for a few seconds. Hit the ground
/// and you crash back to the start. Most points at the bell wins. Layouts: Big Loop, Slalom, Tower.
/// Twist: GOLD RUSH, every ring is worth three.
/// </summary>
public class SkyRingsMinigameController : ScoreMinigameController
{
    private const int RINGS = 18;
    private const float RING_RADIUS = 2.8f, COOLDOWN = 5f;

    [SerializeField] private float _height = 14f;

    public override VehicleKind Vehicle => VehicleKind.Plane;
    public override bool AllowShove => false;
    public override bool UsesDefaultControls => false;
    public override float MaxAltitude => Sky.y + 26f;
    public override string[] Variants => new[] { "Big Loop", "Slalom", "Tower" };
    public override string TwistTitle => "GOLD RUSH";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "Every ring is worth three points.";
    protected override string ScoreUnit => "pts";

    /// <summary>Planes can't use Ice Floor, Giants or Tiny (they don't touch the ground, and size breaks the rings).</summary>
    public override bool SupportsChaos(ChaosModifier modifier) =>
        modifier is not (ChaosModifier.IceFloor or ChaosModifier.Giants or ChaosModifier.Tiny or ChaosModifier.LowGravity or ChaosModifier.SuperJump)
        && base.SupportsChaos(modifier);

    private readonly List<(Vector3 position, Vector3 normal, bool gold)> _rings = new();
    private readonly Dictionary<PlayerRef, Vector3> _last = new();
    private readonly Dictionary<(PlayerRef, int), int> _scoredTick = new();
    private Vector3 Sky => _centre.position + Vector3.up * _height;
    private Vector3 Pad => Sky + Vector3.back * 34f;

    protected override Vector3 ArenaCentre => Sky;

    protected override Vector3 ArenaSpawn(int slot, int count) => Pad + new Vector3((slot - (Mathf.Max(1, count) - 1) * 0.5f) * 3.5f, 0.6f, 0f);

    public override void Spawned()
    {
        base.Spawned();
        // Start pad and a tall glass fence far out (planes turn back off it).
        ArenaProps.Block(transform, PrimitiveType.Cube, Pad + Vector3.down * 0.5f, new Vector3(24f, 0.5f, 8f), ArenaProps.Purple);
        ArenaProps.Fence(transform, _centre.position, 62f, 60f, 56);
        for (int i = 0; i < RINGS; i++)
        {
            float t = i / (float)RINGS * Mathf.PI * 2f;
            Vector3 position;
            switch (Variant)
            {
                case 1:
                    position = Sky + new Vector3(Mathf.Sin(t) * 40f, 5f + Mathf.Sin(t * 3f) * 4f, Mathf.Sin(t * 2f) * 22f);
                    break;
                case 2:
                    position = Sky + new Vector3(Mathf.Cos(t * 2f) * 22f, i * 1.4f, Mathf.Sin(t * 2f) * 22f);
                    break;
                default:
                    position = Sky + new Vector3(Mathf.Sin(t) * 38f, 6f + Mathf.Sin(t * 2f) * 6f, -Mathf.Cos(t) * 30f);
                    break;
            }
            _rings.Add((position, Vector3.zero, i % 5 == 2));
        }
        for (int i = 0; i < RINGS; i++)
        {
            var normal = (_rings[(i + 1) % RINGS].position - _rings[(i + RINGS - 1) % RINGS].position).normalized;
            _rings[i] = (_rings[i].position, normal, _rings[i].gold);
            BuildRing(_rings[i].position, normal, _rings[i].gold);
        }
        ArenaProps.Done();
    }

    private void BuildRing(Vector3 position, Vector3 normal, bool gold)
    {
        var root = new GameObject(gold ? "GoldRing" : "Ring").transform;
        root.SetParent(transform, false);
        root.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
        const int pieces = 16;
        for (int k = 0; k < pieces; k++)
        {
            float a = k / (float)pieces * Mathf.PI * 2f;
            var piece = ArenaProps.Block(root, PrimitiveType.Cube, position, new Vector3(1.15f, 0.35f, 0.35f), gold ? ArenaProps.Yellow : (k % 2 == 0 ? ArenaProps.Pink : Color.white));
            Destroy(piece.GetComponent<Collider>());
            piece.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * RING_RADIUS;
            piece.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
        }
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        _last.Clear();
        _scoredTick.Clear();
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || player.IsSpectator || player.ControlsLocked) { if (entry.Value != null) _last.Remove(entry.Key); continue; }
            var now = player.transform.position + Vector3.up * 0.6f;
            if (_last.TryGetValue(entry.Key, out var before))
                for (int i = 0; i < RINGS; i++)
                {
                    var (center, normal, gold) = _rings[i];
                    float a = Vector3.Dot(before - center, normal), b = Vector3.Dot(now - center, normal);
                    if (a > 0f == b > 0f) continue;   // didn't cross the ring's plane
                    var hit = Vector3.Lerp(before, now, a / (a - b));
                    if ((hit - center).magnitude > RING_RADIUS) continue;
                    if (_scoredTick.TryGetValue((entry.Key, i), out int tick) && Runner.Tick - tick < COOLDOWN * Runner.TickRate) continue;
                    _scoredTick[(entry.Key, i)] = Runner.Tick;
                    int points = gold || TwistActive ? 3 : 1;
                    AddScore(entry.Key, points);
                    RPC_Ring(center, entry.Key, points);
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
    private void RPC_Ring(Vector3 position, PlayerRef player, int points)
    {
        if (player == Runner.LocalPlayer) Sfx.Play(Sound.Safe, 1f, 0.1f);
        WorldPopup.Show(position + Vector3.up * 3.4f, $"+{points}", points > 1 ? new Color(1f, 0.79f, 0.24f) : Color.white, 9f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Crash(Vector3 position)
    {
        Sfx.Play(Sound.Boom, 0.8f, 0.1f);
        LowPolyExplosion.Play(position + Vector3.up, 2f, Resources.Load<Material>("ThrowArc"));
    }
}
