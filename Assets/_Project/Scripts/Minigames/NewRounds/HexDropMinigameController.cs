using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hex Drop: three stacked layers of hexagon tiles high above the studio floor. A tile you step on cracks and drops
/// a moment later, so keep moving and plan your way down; fall through the bottom layer and you are out.
/// Last contestant standing wins. Chaos: during ICE FLOOR or SPEED RUSH tiles drop faster; its own twist,
/// EARTHQUAKE, cracks random tiles on every floor.
/// </summary>
public class HexDropMinigameController : EliminationMinigameController
{
    public const int LAYERS = 3, RINGS = 4;
    private const int PER_LAYER = 1 + 3 * RINGS * (RINGS + 1);   // 61
    public const int MAX_TILES = LAYERS * PER_LAYER;

    [SerializeField] private Transform _centre;
    [SerializeField] private Material _material;
    [SerializeField] private float _hexRadius = 1.7f;
    [SerializeField] private float _layerGap = 6f;
    [SerializeField] private float _baseHeight = 5f;
    [SerializeField] private float _dropDelay = 0.55f;

    /// <summary>Tick the tile was first stepped on (0 = intact).</summary>
    [Networked, Capacity(MAX_TILES)] private NetworkArray<int> SteppedTick { get; }

    private readonly List<Vector3> _tilePositions = new();
    private Transform[] _tiles;
    private Collider[] _colliders;
    private Renderer[] _renderers;
    private byte[] _shown;
    private MaterialPropertyBlock _block;
    private static readonly Color[] LayerColors = { new(1f, 0.49f, 0.71f), new(0.36f, 0.75f, 0.92f), new(1f, 0.79f, 0.24f) };

    private Vector3 Base => _centre.position + Vector3.up * _baseHeight;
    protected override float OutHeight => Base.y - 3f;

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
        return Base + Vector3.up * ((LAYERS - 1) * _layerGap + 1f) + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _hexRadius * 3.5f;
    }

    public override void Spawned()
    {
        base.Spawned();
        Build();
    }

    private void Build()
    {
        _tilePositions.Clear();
        float w = Mathf.Sqrt(3f) * _hexRadius * 0.98f;
        for (int layer = 0; layer < LAYERS; layer++)
            for (int q = -RINGS; q <= RINGS; q++)
                for (int r = Mathf.Max(-RINGS, -q - RINGS); r <= Mathf.Min(RINGS, -q + RINGS); r++)
                {
                    float x = w * (q + r * 0.5f), z = _hexRadius * 1.5f * 0.98f * r;
                    _tilePositions.Add(Base + new Vector3(x, layer * _layerGap, z));
                }

        var mesh = HexMesh(_hexRadius * 0.96f, 0.5f);
        _tiles = new Transform[_tilePositions.Count];
        _colliders = new Collider[_tilePositions.Count];
        _renderers = new Renderer[_tilePositions.Count];
        _shown = new byte[_tilePositions.Count];
        _block = new MaterialPropertyBlock();
        var root = new GameObject("HexTiles").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < _tilePositions.Count; i++)
        {
            var go = new GameObject("Hex " + i, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(root, false);
            go.transform.position = _tilePositions[i];
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = go.GetComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            _tiles[i] = go.transform;
            _colliders[i] = collider;
            _renderers[i] = go.GetComponent<MeshRenderer>();
            _renderers[i].sharedMaterial = _material;
            Tint(i, LayerColors[i / PER_LAYER]);
        }
        Physics.SyncTransforms();   // runtime colliders must reach the physics scene on clients too
    }

    private void Tint(int i, Color color)
    {
        _renderers[i].GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", color);
        _renderers[i].SetPropertyBlock(_block);
    }

    public override string TwistTitle => "EARTHQUAKE";
    public override string TwistDescription => "Random tiles on every floor crack on their own.";

    private float Delay => ActiveChaos is ChaosModifier.IceFloor or ChaosModifier.SpeedRush ? _dropDelay * 0.6f : _dropDelay;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;

        // Earthquake: four times a second one random intact tile per floor cracks.
        if (TwistActive && Runner.Tick % Mathf.Max(1, Runner.TickRate / 4) == 0)
            for (int layer = 0; layer < LAYERS; layer++)
            {
                int index = layer * PER_LAYER + Random.Range(0, PER_LAYER);
                if (SteppedTick[index] == 0) SteppedTick.Set(index, Runner.Tick);
            }

        // Mark the tile under every contestant still in the game.
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || player.ControlsLocked) continue;
            if (!Runner.GetPhysicsScene().Raycast(player.transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            int index = System.Array.IndexOf(_colliders, hit.collider);
            if (index >= 0 && SteppedTick[index] == 0)
                SteppedTick.Set(index, Runner.Tick);
        }
    }

    public override void Render()
    {
        base.Render();
        if (_tiles == null) return;
        int delayTicks = Mathf.CeilToInt(Delay / Runner.DeltaTime);
        for (int i = 0; i < _tiles.Length; i++)
        {
            int stepped = SteppedTick[i];
            byte state = stepped == 0 ? (byte)0 : Runner.Tick - stepped < delayTicks ? (byte)1 : (byte)2;
            if (state == _shown[i]) continue;
            _shown[i] = state;
            if (state == 1)
            {
                Tint(i, new Color(1f, 0.37f, 0.36f));
                _tiles[i].localScale = Vector3.one * 0.94f;
            }
            else if (state == 2)
            {
                _colliders[i].enabled = false;
                _tiles[i].gameObject.SetActive(false);
                Sfx.Play(Sound.Crack, 0.4f, 0.2f);
            }
            else
            {
                _colliders[i].enabled = true;
                _tiles[i].gameObject.SetActive(true);
                _tiles[i].localScale = Vector3.one;
                Tint(i, LayerColors[i / PER_LAYER]);
            }
        }
    }

    // Host physics must lose dropped tiles even when nothing is rendering (e.g. a minimised host window).
    private void LateUpdate()
    {
        if (_tiles == null || !HasStateAuthority || Object == null || !Object.IsValid) return;
        int delayTicks = Mathf.CeilToInt(Delay / Runner.DeltaTime);
        for (int i = 0; i < _tiles.Length; i++)
            if (SteppedTick[i] != 0 && Runner.Tick - SteppedTick[i] >= delayTicks && _colliders[i].enabled)
                _colliders[i].enabled = false;
    }

    public static Mesh HexMesh(float radius, float height)
    {
        var mesh = new Mesh { name = "Hex" };
        var v = new List<Vector3>();
        var t = new List<int>();
        Vector3 Corner(int i, float y) { float a = Mathf.Deg2Rad * (60f * i + 30f); return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius); }
        float top = 0f, bottom = -height;
        // Top and bottom caps.
        foreach (var (y, flip) in new[] { (top, false), (bottom, true) })
        {
            int c = v.Count; v.Add(new Vector3(0f, y, 0f));
            for (int i = 0; i < 6; i++) v.Add(Corner(i, y));
            for (int i = 0; i < 6; i++)
            {
                int a = c + 1 + i, b = c + 1 + (i + 1) % 6;
                if (flip) { t.Add(c); t.Add(a); t.Add(b); } else { t.Add(c); t.Add(b); t.Add(a); }
            }
        }
        // Sides (separate vertices for flat shading).
        for (int i = 0; i < 6; i++)
        {
            int s = v.Count;
            v.Add(Corner(i, top)); v.Add(Corner((i + 1) % 6, top)); v.Add(Corner((i + 1) % 6, bottom)); v.Add(Corner(i, bottom));
            t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
        }
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
