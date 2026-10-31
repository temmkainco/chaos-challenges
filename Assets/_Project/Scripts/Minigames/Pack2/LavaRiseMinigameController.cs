using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lava Rise (after Stumble Guys' Lava Land): lava creeps up from below and speeds up; climb the tower of platforms
/// to stay above it, and shove rivals off the ledges on the way. Touch the lava and you're out. Last one standing
/// wins; survivors tie at the bell. Layouts: Spiral, Twin Spirals, Scatter. Twist: LAVA SURGE, the lava races up.
/// The tower is the same on every peer (built from a fixed seed per layout), so it needs no networking.
/// </summary>
public class LavaRiseMinigameController : EliminationMinigameController
{
    private const float STEP = 1.1f;
    private const int PLATFORMS = 38;
    private const float SIZE = 3f;

    [SerializeField] private Transform _centre;

    /// <summary>Lava surface height, rising at a networked rate (smooth on every screen).</summary>
    [Networked] private NetMotion Lava { get; set; }
    private float LavaTop => Base.y + PLATFORMS * STEP + 2f;
    private float LavaY => Mathf.Min(Lava.AtTick(Runner), LavaTop);

    public override string[] Variants => new[] { "Spiral", "Twin Spirals", "Scatter" };
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
        var bricks = new[] { "BrickPlate", "Brick2", "Brick4", "Brick6" };
        var placed = new List<Vector3> { Base };
        var previous = Vector3.zero;
        // Nothing may hang over a platform within head height, or the jump up bonks into it.
        bool Clear(Vector3 spot) => placed.TrueForAll(p => Mathf.Abs(p.y - spot.y) > 2.4f || new Vector2(p.x - spot.x, p.z - spot.z).magnitude > SIZE + 0.6f);
        for (int i = 1; i <= PLATFORMS; i++)
        {
            float y = Base.y + i * STEP;
            var spots = new List<Vector3>();
            switch (Variant)
            {
                case 1:
                    float a1 = i * 46f * Mathf.Deg2Rad;
                    spots.Add(new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 6.8f);
                    spots.Add(new Vector3(Mathf.Cos(a1 + Mathf.PI), 0f, Mathf.Sin(a1 + Mathf.PI)) * 6.8f);
                    break;
                case 2:
                    // A wandering path: each step within jumping reach of the last, never under another platform.
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        float turn = R(0f, Mathf.PI * 2f);
                        var next = previous + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * R(3.6f, 4.4f);
                        if (next.magnitude > 8.2f || next.magnitude < 3f) continue;
                        if (!Clear(new Vector3(Base.x + next.x, y, Base.z + next.z))) continue;
                        spots.Add(next);
                        break;
                    }
                    if (spots.Count == 0) spots.Add(new Vector3(Mathf.Cos(i * 0.8f), 0f, Mathf.Sin(i * 0.8f)) * 6.8f);
                    break;
                default:
                    float a = i * 46f * Mathf.Deg2Rad;
                    spots.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 6.8f);
                    break;
            }
            foreach (var spot in spots)
            {
                var ground = new Vector3(Base.x + spot.x, y - 0.5f, Base.z + spot.z);
                var block = ArenaProps.Block(transform, PrimitiveType.Cube, ground, new Vector3(SIZE, 0.5f, SIZE), ArenaProps.Pink, R(0f, 90f));
                ArenaProps.Skin(block, bricks[i % bricks.Length]);
                var top = ground + Vector3.up * 0.5f;
                _platforms.Add(top);
                placed.Add(top);
                // A spring every few steps: it throws you up a couple of levels.
                if (i % 7 == 3)
                {
                    _bouncers.Add(top);
                    ArenaModels.Spawn("Bouncer", transform, top + Vector3.up * 0.45f, Quaternion.identity, new Vector3(1.4f, 0.9f, 1.4f), true);
                }
            }
            previous = spots[0];
        }
        // Nobody leaves the tower to wander the studio.
        ArenaProps.Fence(transform, Base, 10.5f, PLATFORMS * STEP + 8f);
        // The tower's spine (it can't be climbed, but it frames the climb).
        ArenaProps.Block(transform, PrimitiveType.Cylinder, Base, new Vector3(2.4f, PLATFORMS * STEP + 3f, 2.4f), new Color(0.55f, 0.5f, 0.7f));
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
