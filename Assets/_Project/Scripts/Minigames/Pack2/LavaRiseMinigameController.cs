using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lava Rise (after Stumble Guys' Lava Land): lava creeps up from below and speeds up; climb the tower of platforms
/// to stay above it, and shove rivals off the ledges on the way. Touch the lava and you're out. Last one standing
/// wins; survivors tie at the bell. The climb is a parkour course, not a staircase: stepping stones, narrow beams,
/// round pillars and wide landings with gaps to jump and rises of different heights, springs now and then. Layouts:
/// Spiral, Twin Spirals, Scatter, Figure Eight. Twist: LAVA SURGE, the lava races up.
/// The course is the same on every peer (built from a fixed seed per layout), so it needs no networking.
/// </summary>
public class LavaRiseMinigameController : EliminationMinigameController
{
    private const int PLATFORMS = 38;
    private const float SIZE = 3f;

    [Header("Parkour course")]
    [SerializeField, Tooltip("Rise between one platform and the next (metres; a jump clears about 1.7)")]
    private Vector2 _rise = new(0.9f, 1.35f);
    [SerializeField, Tooltip("Gap to jump between one platform's edge and the next (metres)")]
    private Vector2 _gap = new(0.6f, 1.8f);
    private float _courseTop;

    [SerializeField] private Transform _centre;

    /// <summary>Lava surface height, rising at a networked rate (smooth on every screen).</summary>
    [Networked] private NetMotion Lava { get; set; }
    private float LavaTop => _courseTop + 2f;
    private float LavaY => Mathf.Min(Lava.AtTick(Runner), LavaTop);

    public override string[] Variants => new[] { "Spiral", "Twin Spirals", "Scatter", "Figure Eight" };
    public override string TwistTitle => "LAVA SURGE";
    public override string TwistDescription => "The lava races up for a few seconds.";

    private readonly List<Vector3> _platforms = new();
    private readonly List<Vector3> _bouncers = new();
    private readonly Dictionary<PlayerRef, int> _bouncedTick = new();
    private Transform _lava;
    private Renderer _lavaRenderer;
    private MaterialPropertyBlock _block;

    private Vector3 Base => _centre.position;
    protected override float OutHeight => LavaY + 0.15f;

    /// <summary>Lowest platform still a safe height above the lava (starts and ghosts).</summary>
    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        if (_platforms.Count == 0) Build();
        foreach (var top in _platforms)
            if (top.y > LavaY + 2f)
            {
                float angle = slot * 2.2f;
                return top + Vector3.up * 0.6f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (top == _platforms[0] ? 3.5f : 0.8f);
            }
        return _platforms[_platforms.Count - 1] + Vector3.up;
    }

    public override void Spawned()
    {
        base.Spawned();
        if (HasStateAuthority) Lava = NetMotion.Still(Runner, Base.y - 4f);
        Build();
    }

    private void Build()
    {
        if (_platforms.Count > 0) return;
        var random = new System.Random(4242 + Variant);
        float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
        // Ground floor everyone starts on.
        _platforms.Add(Base + Vector3.up * 0.4f);
        ArenaProps.Block(transform, PrimitiveType.Cylinder, Base, new Vector3(16f, 0.4f, 16f), ArenaProps.Purple);
        var placed = new List<Vector3> { Base };
        // Nothing may hang over a platform within head height, or the jump up bonks into it. (The piece you jump from is
        // left out: it is behind you and lower.)
        bool Clear(Vector3 spot, float reach, Vector3 from) => placed.TrueForAll(p => p == from || Mathf.Abs(p.y - spot.y) > 2.4f
            || new Vector2(p.x - spot.x, p.z - spot.z).magnitude > reach);

        // One or two paths of pieces; each piece is a few metres on from the last along the path, a little higher.
        int paths = Variant == 1 ? 2 : 1;
        for (int path = 0; path < paths; path++)
        {
            float t = path * Mathf.PI;               // where along its curve the path is
            float y = Base.y;
            var position = PathPoint(t);
            float previousHalf = 1.5f;
            for (int i = 1; i <= PLATFORMS; i++)
            {
                // The piece: wide landings with a spring every few steps; otherwise stones, beams and pillars.
                int kind = i % 7 == 3 ? 3 : random.Next(0, 4);
                var size = kind switch { 0 => new Vector2(1.8f, 1.8f), 1 => new Vector2(1.1f, 4f), 2 => new Vector2(1.5f, 1.5f), _ => new Vector2(3f, 3f) };
                float half = kind == 1 ? size.y * 0.5f : size.x * 0.5f;
                float advance = previousHalf + R(_gap.x, _gap.y) * (kind == 2 ? 0.7f : 1f) + half;
                y += R(_rise.x, _rise.y);
                var from = placed[placed.Count - 1];
                float reach = half + 2f;
                Vector3 next = position;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    if (Variant == 2)
                    {
                        // Scatter: a wandering path, within the fence, never under another piece.
                        float turn = R(0f, Mathf.PI * 2f);
                        next = position + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * advance;
                        if (next.magnitude > 8.2f || next.magnitude < 3f) continue;
                    }
                    else
                    {
                        // Along the curve to the point exactly `advance` away in a straight line (the jump), a little
                        // further each retry if that spot sits under another piece.
                        float want = advance + attempt * 0.6f;
                        float low = 0f, high = Mathf.PI;
                        for (int k = 0; k < 24; k++)
                        {
                            float mid = (low + high) * 0.5f;
                            if ((PathPoint(t + mid) - position).magnitude < want) low = mid; else high = mid;
                        }
                        next = PathPoint(t + high);
                        if (!Clear(new Vector3(Base.x + next.x, y, Base.z + next.z), reach, from) && attempt < 8) continue;
                        t += high;
                        break;
                    }
                    if (!Clear(new Vector3(Base.x + next.x, y, Base.z + next.z), reach, from) && attempt < 29) continue;
                    break;
                }
                var top = new Vector3(Base.x + next.x, y, Base.z + next.z);
                var direction = next - position;
                float yaw = direction.sqrMagnitude > 0.01f ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : 0f;
                PlacePiece(kind, top, size, yaw, i);
                _platforms.Add(top);
                placed.Add(top);
                if (kind == 3 && i % 7 == 3)
                {
                    _bouncers.Add(top);
                    ArenaModels.Spawn("Bouncer", transform, top + Vector3.up * 0.45f, Quaternion.identity, new Vector3(1.4f, 0.9f, 1.4f), true);
                }
                position = next;
                previousHalf = half;
                _courseTop = Mathf.Max(_courseTop, y);
            }
        }
        // Nobody leaves the tower to wander the studio.
        ArenaProps.Fence(transform, Base, 10.5f, _courseTop - Base.y + 8f);
        // The tower's spine (it can't be climbed, but it frames the climb).
        if (Variant != 3)
            ArenaProps.Block(transform, PrimitiveType.Cylinder, Base, new Vector3(2.4f, _courseTop - Base.y + 3f, 2.4f), new Color(0.55f, 0.5f, 0.7f));
        _platforms.Sort((p, q) => p.y.CompareTo(q.y));

        var lava = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lava.name = "Lava";
        Destroy(lava.GetComponent<Collider>());
        lava.transform.SetParent(transform, false);
        lava.transform.localScale = new Vector3(60f, 1f, 60f);
        _lava = lava.transform;
        _lavaRenderer = lava.GetComponent<Renderer>();
        _lavaRenderer.sharedMaterial = ArenaProps.Material;
        _block = new MaterialPropertyBlock();
        ArenaProps.Done();
    }

    /// <summary>The course's line in the plane (relative to the base) for the curved layouts, by parameter t.</summary>
    private Vector3 PathPoint(float t) => Variant == 3
        ? new Vector3(Mathf.Sin(t) * 7.4f, 0f, Mathf.Sin(t * 2f) * 4.4f)   // figure eight
        : new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t)) * 6.8f;               // spiral

    private static readonly string[] Stones = { "BrickPlate", "Brick2" };

    /// <summary>One course piece with its top at <paramref name="top"/>: 0 stone, 1 beam, 2 pillar, 3 wide landing.</summary>
    private void PlacePiece(int kind, Vector3 top, Vector2 size, float yaw, int index)
    {
        if (kind == 2)
        {
            // A round pillar standing up from below, so it reads as a post to hop onto.
            ArenaProps.Block(transform, PrimitiveType.Cylinder, top + Vector3.down * 2.5f, new Vector3(size.x, 2.5f, size.x), ArenaProps.Yellow);
            return;
        }
        var block = ArenaProps.Block(transform, PrimitiveType.Cube, top + Vector3.down * 0.5f, new Vector3(size.x, 0.5f, size.y), ArenaProps.Pink, yaw);
        ArenaProps.Skin(block, kind == 1 ? "BrickLong" : kind == 3 ? (index % 2 == 0 ? "Brick6" : "Brick4") : Stones[index % Stones.Length]);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        float elapsed = Definition.GameDuration * MatchSettings.LengthMultiplier - TimeRemaining;
        float speed = (0.16f + 0.011f * elapsed) * (TwistActive ? 3f : 1f);
        if (LavaY >= LavaTop) speed = 0f;
        // The rate creeps up over the round: re-anchored every half second rather than every tick.
        if (Runner.Tick % (Runner.TickRate / 2) == 0 || speed == 0f && Lava.Rate != 0f) Lava = Lava.Retarget(Runner, speed);
    }

    /// <summary>Springs throw whoever steps on them up to the next level.</summary>
    public override void SimulateContestant(Player player, PlayerMovement movement)
    {
        var who = player.Object.InputAuthority;
        if (HasStateAuthority && !_alive.Contains(who) || CoolingDown(_bouncedTick, who, Runner.TickRate)) return;
        var position = player.transform.position;
        foreach (var spring in _bouncers)
        {
            if (new Vector2(position.x - spring.x, position.z - spring.z).magnitude > 0.9f || position.y < spring.y - 0.2f || position.y > spring.y + 1.2f) continue;
            _bouncedTick[who] = Runner.Tick;
            movement.Launch(Vector3.up * 15f);
            if (HasStateAuthority) RPC_Boing(spring);
            return;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Boing(Vector3 position) => Sfx.Play(Sound.Pop, 1f, 0.15f);

    public override void Render()
    {
        base.Render();
        if (_lava == null) return;
        _lava.position = new Vector3(Base.x, Mathf.Min(Lava.Render(Runner), LavaTop) - 0.5f, Base.z);
        float glow = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        _block.SetColor("_BaseColor", Color.Lerp(new Color(1f, 0.32f, 0.08f), new Color(1f, 0.62f, 0.12f), glow));
        _lavaRenderer.SetPropertyBlock(_block);
    }

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        var local = Player.Local;
        float above = local != null ? local.transform.position.y - LavaY : 0f;
        return above > 0f ? $"{AliveCount} left - lava <b>{above:0}m</b> below you" : $"{AliveCount} left";
    }
}
