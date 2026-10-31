using Fusion;
using System.Collections.Generic;
using UnityEngine;

public struct SlidingWall : INetworkStruct
{
    public int StartTick;
    /// <summary>0..3: which edge it starts from (+X, -X, +Z, -Z travel).</summary>
    public byte Side;
    /// <summary>Gap segments (255 = none); a gap is one segment wide.</summary>
    public byte GapA, GapB;
    /// <summary>Low segments to jump over (255 = none).</summary>
    public byte LowA, LowB;
    /// <summary>Speed in tenths of a metre per second.</summary>
    public short Speed;
}

/// <summary>
/// Block Party (after Fall Guys): toy-brick walls slide across a square platform high above the studio. Stand in a
/// gap or jump a low section as a wall goes by, or it shoves you along until you go over the edge. Walls come faster and from more sides as
/// the round goes on. Last one standing wins. Layouts: Classic (two gaps), Narrow (one gap), Crossfire (walls from
/// two sides). Twist: DOUBLE TROUBLE, two walls at once from opposite sides.
/// </summary>
public class BlockPartyMinigameController : EliminationMinigameController
{
    private const int SEGMENTS = 9, MAX_WALLS = 6;
    private const float SEGMENT = 2f, HALF = SEGMENTS * SEGMENT * 0.5f, WALL_HEIGHT = 3.2f, LOW_HEIGHT = 0.8f;

    [SerializeField] private Transform _centre;
    [SerializeField] private float _height = 6f;

    [Networked, Capacity(MAX_WALLS)] private NetworkArray<SlidingWall> Walls { get; }
    [Networked] private TickTimer NextWall { get; set; }
    [Networked] private int WallCount { get; set; }

    public override string[] Variants => new[] { "Classic", "Narrow", "Crossfire" };
    public override string TwistTitle => "DOUBLE TROUBLE";
    public override string TwistDescription => "Walls come from both sides at once.";

    /// <summary>Per wall: the root (moved as one) and each segment's full and low brick.</summary>
    private Transform[] _wallRoots;
    private GameObject[][] _full, _low;
    private readonly Dictionary<PlayerRef, int> _hitTick = new();

    private Vector3 Floor => _centre.position + Vector3.up * _height;
    protected override float OutHeight => Floor.y - 3f;

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
        return Floor + Vector3.up * 0.5f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 4f;
    }

    public override void Spawned()
    {
        base.Spawned();
        // The platform: a checkerboard of slabs so you can count the segments.
        for (int x = 0; x < SEGMENTS; x++)
            for (int z = 0; z < SEGMENTS; z++)
                ArenaProps.Block(transform, PrimitiveType.Cube, Floor + new Vector3((x + 0.5f) * SEGMENT - HALF, -0.6f, (z + 0.5f) * SEGMENT - HALF),
                    new Vector3(SEGMENT, 0.6f, SEGMENT), (x + z) % 2 == 0 ? ArenaProps.Purple : new Color(0.85f, 0.8f, 0.98f));
        _wallRoots = new Transform[MAX_WALLS];
        _full = new GameObject[MAX_WALLS][];
        _low = new GameObject[MAX_WALLS][];
        var bricks = new[] { "BrickTall", "Brick4", "Brick6", "Brick2" };
        for (int w = 0; w < MAX_WALLS; w++)
        {
            // Built at the origin facing +Z, then moved as one piece.
            var root = new GameObject("Wall " + w).transform;
            root.SetParent(transform, false);
            _wallRoots[w] = root;
            _full[w] = new GameObject[SEGMENTS];
            _low[w] = new GameObject[SEGMENTS];
            for (int seg = 0; seg < SEGMENTS; seg++)
            {
                var x = (seg + 0.5f) * SEGMENT - HALF;
                _full[w][seg] = ArenaModels.Spawn(bricks[(w + seg) % bricks.Length], root, root.TransformPoint(new Vector3(x, WALL_HEIGHT * 0.5f, 0f)), root.rotation,
                    new Vector3(SEGMENT * 0.98f, WALL_HEIGHT, 0.8f))?.gameObject;
                _low[w][seg] = ArenaModels.Spawn("BrickLong", root, root.TransformPoint(new Vector3(x, LOW_HEIGHT * 0.5f, 0f)), root.rotation,
                    new Vector3(SEGMENT * 0.98f, LOW_HEIGHT, 0.8f))?.gameObject;
            }
            root.gameObject.SetActive(false);
        }
        ArenaProps.Done();
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        for (int i = 0; i < MAX_WALLS; i++) Walls.Set(i, default);
        NextWall = TickTimer.CreateFromSeconds(Runner, 1.5f);
        _hitTick.Clear();
    }

    private float Elapsed => Definition.GameDuration * MatchSettings.LengthMultiplier - TimeRemaining;

    /// <summary>Distance the wall has travelled from its start edge (it starts just outside).</summary>
    private float Travel(SlidingWall wall) => (Runner.Tick - wall.StartTick) * Runner.DeltaTime * wall.Speed * 0.1f - 1.5f;

    private static Vector3 Direction(byte side) => side switch { 0 => Vector3.left, 1 => Vector3.right, 2 => Vector3.back, _ => Vector3.forward };

    private void Launch(byte side)
    {
        int slot = -1;
        for (int i = 0; i < MAX_WALLS; i++) if (Walls[i].StartTick == 0) { slot = i; break; }
        if (slot < 0) return;
        // A gap or two to stand in and a couple of low bricks to jump, all on different segments.
        var order = new System.Collections.Generic.List<int>();
        for (int i = 0; i < SEGMENTS; i++) order.Insert(Random.Range(0, order.Count + 1), i);
        byte gapA = (byte)order[0], gapB = Variant == 1 ? (byte)255 : (byte)order[1];
        byte lowA = (byte)order[2], lowB = Random.value < 0.6f ? (byte)order[3] : (byte)255;
        float speed = Mathf.Lerp(3.5f, 7f, Mathf.Clamp01(Elapsed / 70f));
        Walls.Set(slot, new SlidingWall { StartTick = Runner.Tick, Side = side, GapA = gapA, GapB = gapB, LowA = lowA, LowB = lowB, Speed = (short)(speed * 10f) });
        WallCount++;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;

        if (NextWall.Expired(Runner))
        {
            NextWall = TickTimer.CreateFromSeconds(Runner, Mathf.Lerp(4.2f, 2.4f, Mathf.Clamp01(Elapsed / 70f)));
            byte side = (byte)Random.Range(0, Variant == 2 || Elapsed > 30f ? 4 : 2);
            Launch(side);
            if (TwistActive) Launch((byte)(side ^ 1));
            else if (Variant == 2 && Random.value < 0.35f) Launch((byte)((side + 2) % 4));
        }

        for (int w = 0; w < MAX_WALLS; w++)
        {
            var wall = Walls[w];
            if (wall.StartTick == 0) continue;
            float travel = Travel(wall);
            if (travel > SEGMENTS * SEGMENT + 3f) { Walls.Set(w, default); continue; }
            var dir = Direction(wall.Side);
            foreach (var who in _alive)
            {
                if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) continue;
                if (_hitTick.TryGetValue(who, out int last) && Runner.Tick - last < Runner.TickRate / 4) continue;
                var rel = player.transform.position - Floor;
                if (rel.y > WALL_HEIGHT || rel.y < -1f) continue;
                // Along the wall's travel, measured from its start edge; across it, which segment.
                float along = Vector3.Dot(rel, dir) + HALF;
                float across = Vector3.Dot(rel, Vector3.Cross(Vector3.up, dir)) + HALF;
                if (Mathf.Abs(along - travel) > 0.75f) continue;
                int segment = Mathf.FloorToInt(across / SEGMENT);
                if (segment == wall.GapA || segment == wall.GapB) continue;
                // Low bricks only catch you on the ground (jump them).
                if ((segment == wall.LowA || segment == wall.LowB) && rel.y > LOW_HEIGHT - 0.3f) continue;
                if (!player.TryGetComponent<PlayerMovement>(out var movement)) continue;
                _hitTick[who] = Runner.Tick;
                // A shove ahead of the wall, not a launch: it keeps catching you until you find a gap or the edge.
                movement.Launch(dir * (wall.Speed * 0.1f + 3f) + Vector3.up * 2f);
            }
        }
    }

    public override void Render()
    {
        base.Render();
        if (_wallRoots == null) return;
        for (int w = 0; w < MAX_WALLS; w++)
        {
            var wall = Walls[w];
            var root = _wallRoots[w];
            bool on = wall.StartTick != 0 && IsGameActive;
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) continue;
            var dir = Direction(wall.Side);
            float travel = (Runner.LocalRenderTime - wall.StartTick * Runner.DeltaTime) * wall.Speed * 0.1f - 1.5f;
            root.SetPositionAndRotation(Floor + dir * (travel - HALF), Quaternion.LookRotation(dir));
            for (int seg = 0; seg < SEGMENTS; seg++)
            {
                bool gap = seg == wall.GapA || seg == wall.GapB, low = seg == wall.LowA || seg == wall.LowB;
                if (_full[w][seg] != null && _full[w][seg].activeSelf != (!gap && !low)) _full[w][seg].SetActive(!gap && !low);
                if (_low[w][seg] != null && _low[w][seg].activeSelf != low) _low[w][seg].SetActive(low);
            }
        }
    }
}
