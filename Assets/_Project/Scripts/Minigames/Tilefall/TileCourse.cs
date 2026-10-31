using Fusion;
using System.Collections.Generic;
using UnityEngine;

public struct TileState : INetworkStruct
{
    /// <summary>255 = intact, 0 = stepped on (it drops at <see cref="FallTick"/>).</summary>
    public byte Health;
    public NetworkBool Hole;
    public NetworkBool Checkpoint;
    /// <summary>Tick the tile drops (set when it is first stepped on); it comes back a while later.</summary>
    public int FallTick;
}

/// <summary>
/// The Tilefall course: parallel lanes of tiles between a start and a finish platform, built locally on every
/// peer from replicated tile states. The first step on a tile cracks it: it shakes and drops a moment later
/// (<see cref="_fallDelay"/>) whether or not anyone stays on it, so hopping back and forth can't keep a tile up. The
/// drop tick is fixed when it cracks: every peer (clients predict this object) drops its collider on that same tick,
/// so a client never stands on a tile the host has already removed. Tiles come back after a while. Checkpoint rows
/// never crack. Standing on anything that isn't a tile or platform (walls, pipes, wedges) is off the course.
/// Layout is fixed by the serialized settings, so spawn points and the finish line can sit in the scene.
/// </summary>
public class TileCourse : NetworkBehaviour, IBeforeTick
{
    public const int MAX_TILES = 6 * 24;

    [Header("Layout")]
    [SerializeField] private int _lanes = 6;
    [SerializeField] private int _rows = 18;
    [SerializeField] private float _tileSize = 2.6f;
    [SerializeField] private float _gap = 0.35f;
    [SerializeField] private float _tileHeight = 0.8f;
    [SerializeField, Tooltip("Every Nth row is a solid checkpoint row")] private int _checkpointEvery = 6;
    [SerializeField, Range(0f, 0.4f)] private float _holeChance = 0.15f;
    [SerializeField] private float _platformDepth = 6f;

    [Header("Collapse")]
    [SerializeField, Tooltip("Seconds from the first step on a tile to it dropping, whether or not anyone stays on it. " +
        "At least 0.3 s, so every screen learns the drop tick before it comes")]
    private float _fallDelay = 0.55f;
    [SerializeField] private float _respawnSeconds = 4f;

    [Header("Look")]
    [SerializeField] private Material _tileMaterial;
    [SerializeField] private Color[] _laneColors =
    {
        new(0.96f, 0.55f, 0.62f), new(0.55f, 0.78f, 0.98f), new(0.62f, 0.9f, 0.62f),
        new(1f, 0.85f, 0.45f), new(0.78f, 0.62f, 0.96f), new(1f, 0.7f, 0.45f),
    };
    [SerializeField] private Color _checkpointColor = new(0.95f, 0.95f, 0.98f);
    [SerializeField] private Color _platformColor = new(0.8f, 0.7f, 0.95f);
    [SerializeField] private Color _crackColor = new(0.55f, 0.18f, 0.15f);

    [Networked, Capacity(MAX_TILES)] private NetworkArray<TileState> Tiles { get; }
    [Networked] private NetworkBool Generated { get; set; }

    public int Lanes => _lanes;
    public int Rows => _rows;
    private float Pitch => _tileSize + _gap;
    private int TileCount => _lanes * _rows;

    private readonly List<TileView> _views = new();
    /// <summary>Every collider that is part of the course (tiles and the two platforms): anything else is scenery.</summary>
    private readonly HashSet<Collider> _courseColliders = new();
    private int[] _occupants;
    private bool _built;

    #region Layout (same on every peer)

    public Vector3 TilePosition(int lane, int row) =>
        transform.TransformPoint(new Vector3((lane - (_lanes - 1) * 0.5f) * Pitch, 0f, row * Pitch));

    /// <summary>Centre of a lane on the start platform (spawn points).</summary>
    public Vector3 StartPosition(int lane) =>
        transform.TransformPoint(new Vector3((lane - (_lanes - 1) * 0.5f) * Pitch, 0f, -(_platformDepth * 0.5f + Pitch * 0.5f)));

    public Vector3 FinishCenter => transform.TransformPoint(new Vector3(0f, 0f, _rows * Pitch - Pitch * 0.5f + _platformDepth * 0.5f));
    public Vector3 FinishSize => new(_lanes * Pitch, 3f, _platformDepth);

    public bool IsCheckpointRow(int row) => _checkpointEvery > 0 && row % _checkpointEvery == _checkpointEvery - 1;

    /// <summary>Progress along the course in rows (negative on the start platform).</summary>
    public float Progress(Vector3 worldPosition) => transform.InverseTransformPoint(worldPosition).z / Pitch;

    public int LaneAt(Vector3 worldPosition) =>
        Mathf.Clamp(Mathf.RoundToInt(transform.InverseTransformPoint(worldPosition).x / Pitch + (_lanes - 1) * 0.5f), 0, _lanes - 1);

    /// <summary>
    /// Standing somewhere that is not the course: beside the lanes (walls, pipes) or well above the tiles. Players
    /// walked along scenery to skip the whole course.
    /// </summary>
    public bool IsOffCourse(Vector3 worldPosition)
    {
        var local = transform.InverseTransformPoint(worldPosition);
        float halfWidth = _lanes * Pitch * 0.5f + 1f;
        return Mathf.Abs(local.x) > halfWidth || local.y > 2.5f;
    }

    /// <summary>
    /// Host. Feet on something that is not a tile or a platform (side walls, pipes, wedges): players skipped stretches
    /// of the course running along the scenery.
    /// </summary>
    public bool IsOnScenery(Vector3 feet)
    {
        if (!_built) return false;
        int mask = ~((1 << ArenaProps.PLAYER_LAYER) | (1 << ArenaProps.FENCE_LAYER));
        if (!Runner.GetPhysicsScene().Raycast(feet + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, mask, QueryTriggerInteraction.Ignore))
            return false;
        return !_courseColliders.Contains(hit.collider);
    }

    /// <summary>Furthest checkpoint row at or behind <paramref name="row"/>, or -1 for the start platform.</summary>
    public int CheckpointBehind(float row)
    {
        for (int r = Mathf.Min(_rows - 1, Mathf.FloorToInt(row)); r >= 0; r--)
            if (IsCheckpointRow(r)) return r;
        return -1;
    }

    #endregion

    public override void Spawned()
    {
        _occupants = new int[TileCount];

        if (HasStateAuthority && !Generated)
            Generate();
        else if (!HasStateAuthority)
            Runner.SetIsSimulated(Object, true);   // drop tiles on their tick in the client's prediction too
    }

    /// <summary>Whether the tile has dropped at <paramref name="tick"/> (and not come back yet).</summary>
    private bool IsDown(TileState tile, int tick) =>
        tile.Health == 0 && tile.FallTick != 0 && tick >= tile.FallTick && tick - tile.FallTick < RespawnTicks;

    private int RespawnTicks => Mathf.CeilToInt(_respawnSeconds * Runner.TickRate);
    private int FallTicks => Mathf.CeilToInt(Mathf.Max(0.3f, _fallDelay) * Runner.TickRate);

    /// <summary>Every peer, before every tick is simulated (contestants included): colliders follow that tick.</summary>
    public void BeforeTick()
    {
        if (!_built) return;
        for (int i = 0; i < _views.Count; i++)
            if (_views[i] != null)
                _views[i].SetSolid(!IsDown(Tiles[i], Runner.Tick));
    }

    /// <summary>Hole patterns, one per round (<see cref="MinigameController.VariantOf"/>).</summary>
    public static readonly string[] Layouts = { "Classic", "Swiss Cheese", "Stepping Stones", "Snake", "Zigzag" };

    /// <summary>
    /// Snake: a path three lanes wide that swings from side to side. Zigzag: a two-lane band that steps one lane
    /// across every row and bounces off the edges. Both leave the rest of each row open.
    /// </summary>
    private bool IsOnPath(int layout, int lane, int row)
    {
        if (layout == 3)
        {
            float centre = (_lanes - 1) * 0.5f + (_lanes - 3) * 0.5f * Mathf.Sin(row * 0.5f);
            return Mathf.Abs(lane - centre) <= 1.2f;
        }
        int span = Mathf.Max(1, _lanes - 2);
        int position = span - Mathf.Abs(row % (span * 2) - span);
        return lane == position || lane == position + 1;
    }

    private void Generate()
    {
        int layout = MinigameController.VariantOf(Layouts.Length);
        float holeChance = layout == 1 ? _holeChance * 2.2f : _holeChance;
        for (int lane = 0; lane < _lanes; lane++)
        {
            bool previousHole = true; // never open with a hole
            for (int row = 0; row < _rows; row++)
            {
                bool checkpoint = IsCheckpointRow(row);
                bool lastRow = row == _rows - 1;
                // Stepping Stones: a checkerboard, so every step forward is a diagonal hop.
                bool hole = layout switch
                {
                    2 => !checkpoint && !lastRow && (lane + row) % 2 == 1,
                    3 or 4 => !checkpoint && !lastRow && !IsOnPath(layout, lane, row),
                    _ => !checkpoint && !lastRow && !previousHole && Random.value < holeChance,
                };
                previousHole = hole;

                int i = Index(lane, row);
                Tiles.Set(i, new TileState { Health = 255, Hole = hole, Checkpoint = checkpoint });
            }
        }
        Generated = true;
    }

    #region Host simulation

    /// <summary>Host only. Called once per tick with every grounded player still on the course.</summary>
    public void Simulate(IReadOnlyList<Vector3> standingPlayers)
    {
        System.Array.Clear(_occupants, 0, _occupants.Length);
        foreach (var position in standingPlayers)
        {
            int i = TileUnder(position);
            if (i >= 0) _occupants[i]++;
        }


        for (int i = 0; i < TileCount; i++)
        {
            var tile = Tiles[i];
            if (tile.Hole || tile.Checkpoint)
                continue;

            if (tile.Health == 0)
            {
                if (Runner.Tick - tile.FallTick >= RespawnTicks)
                {
                    tile.Health = 255;
                    tile.FallTick = 0;
                    Tiles.Set(i, tile);
                }
                continue;
            }

            // First step: it cracks and drops after the fall delay, whoever stays.
            if (_occupants[i] == 0)
                continue;
            tile.Health = 0;
            tile.FallTick = Runner.Tick + FallTicks;
            Tiles.Set(i, tile);
        }
    }

    private int TileUnder(Vector3 worldPosition)
    {
        var local = transform.InverseTransformPoint(worldPosition);
        if (local.y < -0.5f || local.y > 1.5f)
            return -1;

        int lane = Mathf.RoundToInt(local.x / Pitch + (_lanes - 1) * 0.5f);
        int row = Mathf.RoundToInt(local.z / Pitch);
        if (lane < 0 || lane >= _lanes || row < 0 || row >= _rows)
            return -1;

        var centre = transform.InverseTransformPoint(TilePosition(lane, row));
        float half = _tileSize * 0.5f + 0.25f;
        if (Mathf.Abs(local.x - centre.x) > half || Mathf.Abs(local.z - centre.z) > half)
            return -1;

        return Index(lane, row);
    }

    #endregion

    #region Visuals (every peer)

    public override void Render()
    {
        if (!Generated)
            return;

        if (!_built)
            Build();

        for (int i = 0; i < _views.Count; i++)
        {
            var view = _views[i];
            if (view == null) continue;
            var tile = Tiles[i];
            // Cracked tiles darken and shake more and more until they drop.
            float health = tile.Health == 255 ? 1f : Mathf.Clamp01((tile.FallTick - Runner.Tick) / (float)FallTicks) * 0.6f;
            view.Apply(health, IsDown(tile, Runner.Tick));
        }
    }

    private void Build()
    {
        _built = true;
        var tileMesh = ProceduralMeshes.ChamferedTile(_tileSize, _tileSize, _tileHeight, 0.18f);

        for (int lane = 0; lane < _lanes; lane++)
        {
            for (int row = 0; row < _rows; row++)
            {
                var state = Tiles[Index(lane, row)];
                if (state.Hole)
                {
                    _views.Add(null);
                    continue;
                }

                var color = state.Checkpoint ? _checkpointColor : _laneColors[lane % _laneColors.Length];
                var view = TileView.Create(transform, TilePosition(lane, row), tileMesh, _tileMaterial, color, _crackColor,
                    state.Checkpoint, (lane + row) * 0.015f);
                _views.Add(view);
                _courseColliders.Add(view.GetComponent<Collider>());
            }
        }

        var platformMesh = ProceduralMeshes.ChamferedTile(_lanes * Pitch, _platformDepth, _tileHeight * 1.5f, 0.3f);
        var startCentre = transform.TransformPoint(new Vector3(0f, 0f, -(_platformDepth * 0.5f + Pitch * 0.5f)));
        _courseColliders.Add(TileView.Create(transform, startCentre, platformMesh, _tileMaterial, _platformColor, _crackColor, true, 0f).GetComponent<Collider>());
        _courseColliders.Add(TileView.Create(transform, FinishCenter, platformMesh, _tileMaterial, _checkpointColor, _crackColor, true, 0f).GetComponent<Collider>());
    }

    #endregion

    private int Index(int lane, int row) => lane * _rows + row;
}
