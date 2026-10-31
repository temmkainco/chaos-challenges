using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kart Kup: everyone drives a toy car (W/S throttle and brake, A/D steer, Space hops) three laps round a road-tile
/// track raised above the studio. Yellow pads give a burst of speed; bumping rivals is fair game. Fall off and you're
/// put back on the track. Finishers place in order; at the bell the rest by laps and distance. Layouts: Oval, Hook,
/// Long Oval. Twist: TURBO, everyone gets a speed boost.
/// The track is a loop of 10 m grid cells (straights and corners) built the same way on every peer: a box collider
/// and a road-tile model per cell, a solid wall dressed with fences on every outside edge.
/// </summary>
public class KartKupMinigameController : RaceMinigameController
{
    private const float CELL = 10f, WALL_HEIGHT = 1.4f;
    private const int LAPS = 3;

    [SerializeField] private float _height = 12f;

    public override VehicleKind Vehicle => VehicleKind.Kart;
    public override bool AllowShove => false;
    public override bool UsesDefaultControls => false;
    public override string[] Variants => new[] { "Oval", "Hook", "Long Oval" };
    public override string TwistTitle => "TURBO";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "Everyone gets a burst of speed.";

    private readonly List<Vector2Int> _cells = new();
    private readonly List<int> _pads = new();
    private readonly Dictionary<PlayerRef, int> _index = new();
    private readonly Dictionary<PlayerRef, int> _lap = new();
    private Vector3 _origin;

    private int Count => _cells.Count;
    protected override float FallHeight => _origin.y - 5f;

    private static readonly Vector2Int[] Oval = Loop(6, 3);
    private static readonly Vector2Int[] LongOval = Loop(8, 2);
    private static readonly Vector2Int[] Hook =
    {
        new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(4, 1), new(4, 2), new(3, 2), new(2, 2), new(2, 3),
        new(2, 4), new(1, 4), new(0, 4), new(0, 3), new(0, 2), new(0, 1),
    };

    /// <summary>The cells round the edge of a w x h rectangle, in driving order.</summary>
    private static Vector2Int[] Loop(int w, int h)
    {
        var cells = new List<Vector2Int>();
        for (int x = 0; x < w; x++) cells.Add(new Vector2Int(x, 0));
        for (int z = 1; z < h; z++) cells.Add(new Vector2Int(w - 1, z));
        for (int x = w - 2; x >= 0; x--) cells.Add(new Vector2Int(x, h - 1));
        for (int z = h - 2; z >= 1; z--) cells.Add(new Vector2Int(0, z));
        return cells.ToArray();
    }

    private Vector3 CellCentre(int i) => _origin + new Vector3(_cells[(i % Count + Count) % Count].x * CELL, 0f, _cells[(i % Count + Count) % Count].y * CELL);
    private Vector3 Forward(int i) => (CellCentre(i + 1) - CellCentre(i)).normalized;

    public override void Spawned()
    {
        base.Spawned();
        var loop = Variant switch { 1 => Hook, 2 => LongOval, _ => Oval };
        // Start the list on a straight with a straight behind it (the grid lines up there).
        int start = 0;
        for (int i = 0; i < loop.Length; i++)
        {
            var a = loop[(i - 1 + loop.Length) % loop.Length]; var b = loop[i]; var c = loop[(i + 1) % loop.Length]; var d = loop[(i + 2) % loop.Length];
            if (b - a == c - b && c - b == d - c) { start = i; break; }
        }
        for (int i = 0; i < loop.Length; i++) _cells.Add(loop[(start + i) % loop.Length]);
        var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
        foreach (var c in _cells) { min = Vector2.Min(min, c); max = Vector2.Max(max, c); }
        var middle = (min + max) * 0.5f * CELL;
        _origin = _centre.position + Vector3.up * _height - new Vector3(middle.x, 0f, middle.y);
        Build();
    }

    private void Build()
    {
        var neighbours = new HashSet<(Vector2Int, Vector2Int)>();
        for (int i = 0; i < Count; i++)
        {
            neighbours.Add((_cells[i], _cells[(i + 1) % Count]));
            neighbours.Add((_cells[(i + 1) % Count], _cells[i]));
        }
        var sides = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        for (int i = 0; i < Count; i++)
        {
            var cell = _cells[i];
            var centre = CellCentre(i);
            var floor = ArenaProps.Block(transform, PrimitiveType.Cube, centre + Vector3.down * 0.6f, new Vector3(CELL, 0.6f, CELL), new Color(0.3f, 0.3f, 0.36f));
            floor.GetComponent<Renderer>().enabled = false;
            var into = cell - _cells[(i - 1 + Count) % Count];
            var outOf = _cells[(i + 1) % Count] - cell;
            bool straight = into == outOf;
            // The road tiles run along their local X (straights) and turn from -X round to -Z (corners).
            float yaw;
            if (straight) yaw = outOf.x != 0 ? 0f : 90f;
            else yaw = CornerYaw(-into, outOf);
            ArenaModels.Spawn(straight ? "RoadStraight" : "RoadCorner", transform, centre + Vector3.down * 0.08f, Quaternion.Euler(0f, yaw, 0f), new Vector3(CELL, 0.16f, CELL));

            // Walls on every edge that doesn't lead to the next or previous cell.
            foreach (var side in sides)
            {
                if (neighbours.Contains((cell, cell + side))) continue;
                var normal = new Vector3(side.x, 0f, side.y);
                var wallCentre = centre + normal * (CELL * 0.5f + 0.25f);
                var wall = ArenaProps.Block(transform, PrimitiveType.Cube, wallCentre, new Vector3(CELL + 0.5f, WALL_HEIGHT, 0.5f), Color.white,
                    Quaternion.LookRotation(normal).eulerAngles.y);
                wall.GetComponent<Renderer>().enabled = false;
                var along = Vector3.Cross(Vector3.up, normal);
                for (int f = 0; f < 5; f++)
                    ArenaModels.Spawn("WoodFence", transform, wallCentre + along * ((f - 2f) * 2f) + Vector3.up * WALL_HEIGHT * 0.5f,
                        Quaternion.LookRotation(normal), new Vector3(2f, WALL_HEIGHT, 0.15f));   // fence runs along its X = along
            }
        }
        // Start line, speed pads on two straights (no colliders: the host checks who drives over them).
        var line = ArenaProps.Block(transform, PrimitiveType.Cube, CellCentre(0) + Vector3.up * 0.03f, new Vector3(CELL, 0.03f, 0.8f), Color.white,
            Quaternion.LookRotation(Forward(0)).eulerAngles.y);
        Destroy(line.GetComponent<Collider>());
        ArenaModels.Spawn("Flag", transform, CellCentre(0) + Vector3.Cross(Vector3.up, Forward(0)) * (CELL * 0.5f - 0.6f) + Vector3.up * 1.2f,
            Quaternion.LookRotation(Forward(0)), new Vector3(1.4f, 2.4f, 0.4f), true);
        for (int i = 2; i < Count; i++)
        {
            if (_pads.Count >= 2) break;
            if (Forward(i - 1) != Forward(i) || (_pads.Count > 0 && i - _pads[0] < Count / 3)) continue;
            _pads.Add(i);
            var pad = ArenaProps.Block(transform, PrimitiveType.Cube, CellCentre(i) + Vector3.up * 0.04f, new Vector3(CELL * 0.4f, 0.05f, 3f), ArenaProps.Yellow,
                Quaternion.LookRotation(Forward(i)).eulerAngles.y);
            Destroy(pad.GetComponent<Collider>());
        }
        ArenaProps.Done();
    }

    /// <summary>Yaw for a corner tile joining the sides <paramref name="a"/> and <paramref name="b"/> of the cell.</summary>
    private static float CornerYaw(Vector2Int a, Vector2Int b)
    {
        bool Has(Vector2Int side) => a == side || b == side;
        if (Has(Vector2Int.left) && Has(Vector2Int.down)) return 0f;
        if (Has(Vector2Int.left) && Has(Vector2Int.up)) return 90f;
        if (Has(Vector2Int.right) && Has(Vector2Int.up)) return 180f;
        return 270f;
    }

    protected override Vector3 StartPosition(int slot, int count, out float yaw)
    {
        var forward = Forward(0);
        yaw = Quaternion.LookRotation(forward).eulerAngles.y;
        int row = slot / 2, column = slot % 2;
        return CellCentre(0) - forward * (2.5f + row * 3.5f) + Vector3.Cross(Vector3.up, forward) * (column == 0 ? -2.2f : 2.2f) + Vector3.up * 0.6f;
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        _index.Clear();
        _lap.Clear();
    }

    /// <summary>The cell under <paramref name="position"/>, searched near the last one (cells never repeat).</summary>
    private int Nearest(PlayerRef player, Vector3 position)
    {
        bool known = _index.TryGetValue(player, out int last);
        int best = 0;
        float bestDistance = float.MaxValue;
        int from = known ? last - 2 : 0, to = known ? last + 3 : Count;
        for (int k = from; k < to; k++)
        {
            int i = (k % Count + Count) % Count;
            var offset = position - CellCentre(i);
            float d = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.z));
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    /// <summary>Laps times cells plus the cell and how far through it. The grid starts behind the line on lap -1.</summary>
    protected override float Progress(PlayerRef player, Vector3 position)
    {
        int index = Nearest(player, position);
        if (!_lap.TryGetValue(player, out int lap)) lap = Vector3.Dot(position - CellCentre(0), Forward(0)) < 0f ? -1 : 0;
        if (_index.TryGetValue(player, out int last))
        {
            if (last > Count * 3 / 4 && index < Count / 4) lap++;
            else if (last < Count / 4 && index > Count * 3 / 4) lap--;
        }
        _index[player] = index;
        _lap[player] = lap;
        float through = Mathf.Clamp(Vector3.Dot(position - CellCentre(index), Forward(index)) / CELL, -0.5f, 0.5f);
        return lap * Count + index + through;
    }

    protected override bool IsFinished(float progress) => progress >= LAPS * Count;

    protected override string ProgressLabel(float progress) => $"Lap {Mathf.Clamp(Mathf.FloorToInt(progress / Mathf.Max(1, Count)) + 1, 1, LAPS)}/{LAPS}";

    protected override Vector3 RespawnPoint(PlayerRef player, float bestProgress, out float yaw)
    {
        int i = _index.TryGetValue(player, out int last) ? last : 0;
        yaw = Quaternion.LookRotation(Forward(i)).eulerAngles.y;
        return CellCentre(i) + Vector3.up * 0.6f;
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier != ChaosModifier.Twist) return;
        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null && entry.Value.TryGetComponent<PlayerMovement>(out var movement)) movement.Boost(4f);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || !_index.TryGetValue(entry.Key, out int index) || !_pads.Contains(index)) continue;
            var rel = player.transform.position - CellCentre(index);
            if (Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, Forward(index)))) < CELL * 0.22f && Mathf.Abs(Vector3.Dot(rel, Forward(index))) < 2f
                && player.TryGetComponent<PlayerMovement>(out var movement) && !movement.IsBoosted)
                movement.Boost(1.4f);
        }
    }
}
