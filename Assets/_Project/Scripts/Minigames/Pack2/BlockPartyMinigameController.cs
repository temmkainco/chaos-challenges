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
    /// <summary>Speed in tenths of a metre per second.</summary>
    public short Speed;
}

/// <summary>
/// Block Party (after Fall Guys): walls with gaps slide across a square platform high above the studio. Stand in a
/// gap as a wall goes by, or it shoves you along until you go over the edge. Walls come faster and from more sides as
/// the round goes on. Last one standing wins. Layouts: Classic (two gaps), Narrow (one gap), Crossfire (walls from
/// two sides). Twist: DOUBLE TROUBLE, two walls at once from opposite sides.
/// </summary>
public class BlockPartyMinigameController : EliminationMinigameController
{
    private const int SEGMENTS = 9, MAX_WALLS = 6;
    private const float SEGMENT = 2f, HALF = SEGMENTS * SEGMENT * 0.5f, WALL_HEIGHT = 3.2f;

    [SerializeField] private Transform _centre;
    [SerializeField] private float _height = 6f;

    [Networked, Capacity(MAX_WALLS)] private NetworkArray<SlidingWall> Walls { get; }
    [Networked] private TickTimer NextWall { get; set; }
    [Networked] private int WallCount { get; set; }

    public override string[] Variants => new[] { "Classic", "Narrow", "Crossfire" };
    public override string TwistTitle => "DOUBLE TROUBLE";
    public override string TwistDescription => "Walls come from both sides at once.";

    private Transform[][] _wallViews;
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
        _wallViews = new Transform[MAX_WALLS][];
        var colors = new[] { ArenaProps.Pink, ArenaProps.Blue, ArenaProps.Yellow, ArenaProps.Green };
        for (int w = 0; w < MAX_WALLS; w++)
        {
            var root = new GameObject("Wall " + w).transform;
            root.SetParent(transform, false);
            _wallViews[w] = new Transform[SEGMENTS + 1];
            _wallViews[w][SEGMENTS] = root;
            for (int s = 0; s < SEGMENTS; s++)
            {
                var piece = ArenaProps.Block(root, PrimitiveType.Cube, Floor, new Vector3(SEGMENT * 0.98f, WALL_HEIGHT, 0.8f), colors[w % colors.Length]);
                Destroy(piece.GetComponent<Collider>());   // the host pushes; no collider for the controller to snag on
                _wallViews[w][s] = piece;
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
        byte gapA = (byte)Random.Range(0, SEGMENTS), gapB = 255;
        if (Variant != 1)
        {
            gapB = (byte)Random.Range(0, SEGMENTS - 1);
            if (gapB >= gapA) gapB++;
        }
        float speed = Mathf.Lerp(3.5f, 7f, Mathf.Clamp01(Elapsed / 70f));
        Walls.Set(slot, new SlidingWall { StartTick = Runner.Tick, Side = side, GapA = gapA, GapB = gapB, Speed = (short)(speed * 10f) });
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
        if (_wallViews == null) return;
        for (int w = 0; w < MAX_WALLS; w++)
        {
            var wall = Walls[w];
            var root = _wallViews[w][SEGMENTS];
            bool on = wall.StartTick != 0 && IsGameActive;
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) continue;
            var dir = Direction(wall.Side);
            var across = Vector3.Cross(Vector3.up, dir);
            float travel = (Runner.LocalRenderTime - wall.StartTick * Runner.DeltaTime) * wall.Speed * 0.1f - 1.5f;
            var origin = Floor + dir * (travel - HALF);
            for (int s = 0; s < SEGMENTS; s++)
            {
                var piece = _wallViews[w][s];
                bool gap = s == wall.GapA || s == wall.GapB;
                if (piece.gameObject.activeSelf == gap) piece.gameObject.SetActive(!gap);
                piece.SetPositionAndRotation(origin + across * ((s + 0.5f) * SEGMENT - HALF) + Vector3.up * WALL_HEIGHT * 0.5f, Quaternion.LookRotation(dir));
            }
        }
    }
}
