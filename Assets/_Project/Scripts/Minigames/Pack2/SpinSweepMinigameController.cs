using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spin Sweep (after Fall Guys' Jump Showdown): a round floor of hexagon tiles high above the studio while bars sweep
/// round just above it, faster and faster. Jump each bar or get swept off; every few seconds some tiles crack and
/// drop. Last one standing wins. Layouts: Two Bars, Cross, Counter-Spin (two bars turning opposite ways).
/// Twist: OVERDRIVE, the bars spin twice as fast.
/// </summary>
public class SpinSweepMinigameController : EliminationMinigameController, IBeforeTick
{
    private const int RINGS = 4, TILES = 61;
    private const float BAR_TOP = 0.6f;

    [SerializeField] private Transform _centre;
    [SerializeField] private float _height = 6f;
    [SerializeField] private float _hexRadius = 1.7f;

    /// <summary>Bar angles in degrees (A turns forward, B backward in Counter-Spin).</summary>
    /// <summary>The bars' yaw in degrees, evaluated per tick and per frame (smooth on every screen).</summary>
    [Networked] private NetMotion BarA { get; set; }
    [Networked] private NetMotion BarB { get; set; }
    /// <summary>Tick a tile cracked (0 = intact).</summary>
    [Networked, Capacity(64)] private NetworkArray<int> CrackTick { get; }
    [Networked] private TickTimer NextCrack { get; set; }

    public override string[] Variants => new[] { "Two Bars", "Cross", "Counter-Spin" };
    public override string TwistTitle => "OVERDRIVE";
    public override string TwistDescription => "The bars spin twice as fast for a while.";
    public override bool AllowsSprint => false;

    private readonly List<Vector3> _tilePositions = new();
    private Transform[] _tiles;
    private Collider[] _colliders;
    private byte[] _shown;
    private Transform _barsA, _barsB;
    private readonly Dictionary<PlayerRef, int> _hitTick = new();
    private MaterialPropertyBlock _block;

    private Vector3 Floor => _centre.position + Vector3.up * _height;
    private float Reach => (RINGS + 0.6f) * _hexRadius * 1.5f;
    protected override float OutHeight => Floor.y - 3f;

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        // Between the bars (they start along the axes), never on one.
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f + Mathf.PI * 0.25f;
        return Floor + Vector3.up * 0.5f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Reach * 0.55f;
    }

    public override void Spawned()
    {
        base.Spawned();
        _block = new MaterialPropertyBlock();
        float w = Mathf.Sqrt(3f) * _hexRadius * 0.98f;
        for (int q = -RINGS; q <= RINGS; q++)
            for (int r = Mathf.Max(-RINGS, -q - RINGS); r <= Mathf.Min(RINGS, -q + RINGS); r++)
                _tilePositions.Add(Floor + new Vector3(w * (q + r * 0.5f), 0f, _hexRadius * 1.5f * 0.98f * r));
        var mesh = HexDropMinigameController.HexMesh(_hexRadius * 0.96f, 0.5f);
        _tiles = new Transform[_tilePositions.Count];
        _colliders = new Collider[_tilePositions.Count];
        _shown = new byte[_tilePositions.Count];
        for (int i = 0; i < _tilePositions.Count; i++)
        {
            var go = new GameObject("Tile " + i, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(transform, false);
            go.transform.position = _tilePositions[i];
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = go.GetComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            go.GetComponent<MeshRenderer>().sharedMaterial = ArenaProps.Material;
            _tiles[i] = go.transform;
            _colliders[i] = collider;
            Tint(i, TileColor(i));
        }

        // The hub the bars turn on (you can't stand in the middle) and the bars themselves.
        ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cylinder, Floor, new Vector3(2.4f, 1.6f, 2.4f), ArenaProps.Yellow), "Tower", true);
        _barsA = BuildBars(Variant == 1 ? 4 : Variant == 2 ? 1 : 2, ArenaProps.Pink);
        if (Variant == 2) _barsB = BuildBars(1, ArenaProps.Blue);
        ArenaProps.Done();
    }

    private Color TileColor(int i) => (i % 3) switch { 0 => ArenaProps.Purple, 1 => ArenaProps.Blue, _ => ArenaProps.Green };

    private Transform BuildBars(int arms, Color color)
    {
        var root = new GameObject("Bars").transform;
        root.SetParent(transform, false);
        root.position = Floor;
        for (int a = 0; a < arms; a++)
        {
            var arm = new GameObject("Arm").transform;
            arm.SetParent(root, false);
            arm.localRotation = Quaternion.Euler(0f, a * 360f / arms, 0f);
            // A log for each arm (its long side along the arm), low enough to jump.
            float length = Reach - 1.1f;
            ArenaModels.Spawn("Log", arm, arm.TransformPoint(new Vector3(0f, BAR_TOP - 0.25f, 1.1f + length * 0.5f)), arm.rotation * Quaternion.Euler(0f, 90f, 0f),
                new Vector3(length, 0.5f, 0.5f), tint: color);
        }
        return root;
    }

    private float Arms => Variant == 1 ? 4 : Variant == 2 ? 1 : 2;

    /// <summary>Degrees per second, ramping up over the round.</summary>
    private float Speed()
    {
        float total = Definition.GameDuration * MatchSettings.LengthMultiplier;
        float elapsed = IsGameActive ? total - TimeRemaining : 0f;
        float speed = Mathf.Lerp(50f, 150f, Mathf.Clamp01(elapsed / 70f));
        if (Variant == 1) speed *= 0.6f;    // four arms come round often enough
        return speed * (TwistActive ? 2f : 1f);
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        NextCrack = TickTimer.CreateFromSeconds(Runner, 6f);
        _hitTick.Clear();
    }

    protected override bool SimulatedOnClients => true;

    private int CrackTicks => Mathf.CeilToInt(1.3f / Runner.DeltaTime);

    /// <summary>Every peer, before every tick is simulated (contestants included): cracked tiles stop being solid on their drop tick.</summary>
    public void BeforeTick()
    {
        if (_colliders == null) return;
        for (int i = 0; i < _colliders.Length; i++)
        {
            bool solid = CrackTick[i] == 0 || Runner.Tick - CrackTick[i] < CrackTicks;
            if (_colliders[i].enabled != solid) _colliders[i].enabled = solid;
        }
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!IsGameActive) return;
        if (HasStateAuthority)
        {
            float speed = Speed();
            // Re-anchored only when the speed has crept up noticeably, so clients rarely have to correct the bars.
            if (Mathf.Abs(BarA.Rate - speed) > 1f) BarA = BarA.Retarget(Runner, speed, 360f);
            if (Variant == 2 && Mathf.Abs(BarB.Rate + speed * 0.8f) > 1f) BarB = BarB.Retarget(Runner, -speed * 0.8f, 360f);
        }
        float angleA = BarA.AtTick(Runner), angleB = BarB.AtTick(Runner);
        float prevA = angleA - BarA.Rate * Runner.DeltaTime, prevB = angleB - BarB.Rate * Runner.DeltaTime;

        // Tiles: a few crack every few seconds (never the ones under the hub), then drop.
        if (HasStateAuthority && NextCrack.Expired(Runner))
        {
            NextCrack = TickTimer.CreateFromSeconds(Runner, Mathf.Max(2.5f, 6f - (Definition.GameDuration - TimeRemaining) * 0.04f));
            for (int n = 0, guard = 0; n < 3 && guard < 40; guard++)
            {
                int i = Random.Range(0, _tilePositions.Count);
                if (CrackTick[i] != 0 || (_tilePositions[i] - Floor).magnitude < _hexRadius * 1.2f) continue;
                CrackTick.Set(i, Runner.Tick);
                n++;
            }
        }

        // Sweep: anyone standing low where a bar passed this tick is flung off.
        foreach (var who in HitTestedContestants(_alive))
        {
            if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) continue;
            if (CoolingDown(_hitTick, who, Runner.TickRate / 2)) continue;
            var rel = player.transform.position - Floor;
            float radius = new Vector2(rel.x, rel.z).magnitude;
            if (radius < 1.1f || radius > Reach + 0.5f || rel.y > BAR_TOP + 0.05f) continue;
            float angle = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;   // same convention as the bars' yaw
            float halfWidth = Mathf.Rad2Deg * 0.55f / radius;
            int dir = 0;
            for (int a = 0; a < Arms; a++)
            {
                float offset = a * 360f / Arms;
                if (Swept(prevA + offset, angleA + offset, angle, halfWidth)) dir = 1;
            }
            if (Variant == 2 && Swept(prevB, angleB, angle, halfWidth)) dir = -1;
            if (dir == 0 || !player.TryGetComponent<PlayerMovement>(out var movement)) continue;
            _hitTick[who] = Runner.Tick;
            var outward = new Vector3(rel.x, 0f, rel.z).normalized;
            var tangent = Vector3.Cross(Vector3.up, outward) * dir;
            movement.Launch(tangent * 6f + outward * 2f + Vector3.up * 5.5f);
            if (HasStateAuthority) RPC_Swept(player.transform.position);
        }
    }

    /// <summary>Whether a bar turning from <paramref name="from"/> to <paramref name="to"/> passed <paramref name="target"/>.</summary>
    private static bool Swept(float from, float to, float target, float halfWidth)
    {
        float travel = Mathf.DeltaAngle(from, to);
        float start = Mathf.DeltaAngle(from, target);
        if (travel >= 0f) return start >= -halfWidth && start <= travel + halfWidth;
        return start <= halfWidth && start >= travel - halfWidth;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Swept(Vector3 position)
    {
        Sfx.Play(Sound.Whoosh, 0.9f, 0.1f);
        WorldPopup.Show(position + Vector3.up * 2.4f, "SWEPT!", new Color(1f, 0.49f, 0.71f), 8f);
    }

    public override void Render()
    {
        base.Render();
        if (_barsA == null) return;
        _barsA.rotation = Quaternion.Euler(0f, BarA.Render(Runner), 0f);
        if (_barsB != null) _barsB.rotation = Quaternion.Euler(0f, BarB.Render(Runner), 0f);
        for (int i = 0; i < _tiles.Length; i++)
        {
            int tick = CrackTick[i];
            byte state = tick == 0 ? (byte)0 : Runner.Tick - tick < CrackTicks ? (byte)1 : (byte)2;
            if (state == _shown[i]) continue;
            _shown[i] = state;
            _tiles[i].GetComponent<Renderer>().enabled = state != 2;
            Tint(i, state == 1 ? new Color(1f, 0.37f, 0.36f) : TileColor(i));
            if (state == 2) Sfx.Play(Sound.Crack, 0.4f, 0.2f);
        }
    }

    private void Tint(int i, Color color)
    {
        var renderer = _tiles[i].GetComponent<Renderer>();
        renderer.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(_block);
    }
}
