using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Paint Party: every floor tile you step on turns your colour, even ones a rival painted. Most tiles in your colour
/// at the bell wins. Everyone gets their own paint colour (shown under their feet and on the HUD), not their outfit
/// colour, so two contestants never share one. Layouts: Square, Ring, Islands. Twist: SPLAT, the player in last
/// place gets three big splats of paint across the floor.
/// </summary>
public class PaintPartyMinigameController : ScoreMinigameController
{
    private const int GRID = 16;
    private const float TILE = 1.4f;

    [SerializeField] private float _height = 3f;

    /// <summary>Owner per tile: 0 = unpainted, otherwise PlayerId + 1.</summary>
    [Networked, Capacity(GRID * GRID)] private NetworkArray<byte> Owner { get; }

    public override string[] Variants => new[] { "Square", "Ring", "Islands" };
    public override string TwistTitle => "SPLAT";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "Whoever is last gets three big splats of paint.";
    protected override string ScoreUnit => "tiles";

    public static readonly Color[] Paints =
    {
        new(1f, 0.33f, 0.5f), new(0.25f, 0.6f, 1f), new(1f, 0.78f, 0.15f), new(0.25f, 0.82f, 0.45f),
        new(0.68f, 0.42f, 1f), new(1f, 0.55f, 0.2f), new(0.2f, 0.85f, 0.85f), new(0.95f, 0.95f, 0.95f),
    };
    private static readonly string[] PaintNames = { "PINK", "BLUE", "YELLOW", "GREEN", "PURPLE", "ORANGE", "TEAL", "WHITE" };

    private readonly List<int> _tileIndex = new();     // grid cell -> tile number, -1 when there is no tile
    private Renderer[] _renderers;
    private byte[] _shown;
    private MaterialPropertyBlock _block;
    private readonly Dictionary<PlayerRef, Transform> _markers = new();

    private Vector3 Floor => ArenaCentre + Vector3.up * _height;
    protected override float FallHeight => Floor.y - 2f;   // the studio floor is only 3.6 m below

    private bool HasTile(int x, int z) => Variant switch
    {
        1 => !(x >= 5 && x < 11 && z >= 5 && z < 11),
        2 => x != 7 && x != 8 && z != 7 && z != 8,
        _ => true,
    };

    private Vector3 CellPosition(int x, int z) => Floor + new Vector3((x - (GRID - 1) * 0.5f) * TILE, 0f, (z - (GRID - 1) * 0.5f) * TILE);

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
        var spot = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * GRID * TILE * 0.36f;
        return Floor + spot + Vector3.up * 0.6f;
    }

    /// <summary>Paint colour by join order, the same on every peer.</summary>
    private int PaintIndex(PlayerRef player)
    {
        int index = ConnectedPlayers().OrderBy(p => p.PlayerId).ToList().IndexOf(player);
        return index < 0 ? 0 : index % Paints.Length;
    }

    public override void Spawned()
    {
        base.Spawned();
        _block = new MaterialPropertyBlock();
        var renderers = new List<Renderer>();
        for (int z = 0; z < GRID; z++)
            for (int x = 0; x < GRID; x++)
            {
                if (!HasTile(x, z)) { _tileIndex.Add(-1); continue; }
                _tileIndex.Add(renderers.Count);
                var tile = ArenaProps.Block(transform, PrimitiveType.Cube, CellPosition(x, z) + Vector3.down * 0.35f, new Vector3(TILE * 0.96f, 0.35f, TILE * 0.96f), Color.white);
                renderers.Add(tile.GetComponent<Renderer>());
            }
        _renderers = renderers.ToArray();
        _shown = new byte[_renderers.Length];
        for (int i = 0; i < _shown.Length; i++) _shown[i] = 255;
        ArenaProps.Done();
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        for (int i = 0; i < GRID * GRID; i++) Owner.Set(i, 0);
    }

    private int CellAt(Vector3 position)
    {
        var local = position - Floor;
        int x = Mathf.RoundToInt(local.x / TILE + (GRID - 1) * 0.5f), z = Mathf.RoundToInt(local.z / TILE + (GRID - 1) * 0.5f);
        if (x < 0 || z < 0 || x >= GRID || z >= GRID || local.y < -0.6f || local.y > 1.2f || !HasTile(x, z)) return -1;
        return z * GRID + x;
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier != ChaosModifier.Twist) return;
        var last = ConnectedPlayers().OrderBy(ScoreOf).FirstOrDefault();
        if (last == PlayerRef.None) return;
        for (int s = 0; s < 3; s++)
        {
            int cx = Random.Range(2, GRID - 2), cz = Random.Range(2, GRID - 2);
            for (int z = cz - 2; z <= cz + 2; z++)
                for (int x = cx - 2; x <= cx + 2; x++)
                    if (x >= 0 && z >= 0 && x < GRID && z < GRID && HasTile(x, z) && (x - cx) * (x - cx) + (z - cz) * (z - cz) <= 5)
                        Owner.Set(z * GRID + x, (byte)(last.PlayerId + 1));
        }
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || player.IsSpectator || player.ControlsLocked) continue;
            int cell = CellAt(player.transform.position);
            if (cell >= 0) Owner.Set(cell, (byte)(entry.Key.PlayerId + 1));
        }
        // The score is the number of tiles you own right now.
        if (Runner.Tick % 10 != 0) return;
        var counts = new Dictionary<int, int>();
        for (int i = 0; i < GRID * GRID; i++)
            if (Owner[i] != 0) counts[Owner[i]] = counts.TryGetValue(Owner[i], out int n) ? n + 1 : 1;
        foreach (var player in ConnectedPlayers())
            SetScore(player, counts.TryGetValue(player.PlayerId + 1, out int c) ? c : 0);
    }

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        int paint = PaintIndex(Runner.LocalPlayer);
        return $"You paint <color=#{ColorUtility.ToHtmlStringRGB(Paints[paint] * 0.8f)}><b>{PaintNames[paint]}</b></color>   " + base.GetStatusText();
    }

    public override void Render()
    {
        base.Render();
        if (_renderers == null) return;
        var colorOf = new Dictionary<int, Color>();
        foreach (var player in ConnectedPlayers()) colorOf[player.PlayerId + 1] = Paints[PaintIndex(player)];
        for (int cell = 0; cell < GRID * GRID; cell++)
        {
            int tile = _tileIndex[cell];
            if (tile < 0) continue;
            byte owner = Owner[cell];
            if (owner == _shown[tile]) continue;
            _shown[tile] = owner;
            _renderers[tile].GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", owner == 0 || !colorOf.TryGetValue(owner, out var c) ? new Color(0.93f, 0.92f, 0.96f) : c);
            _renderers[tile].SetPropertyBlock(_block);
        }
        // A disc of your paint under every contestant.
        foreach (var entry in _playerSpawner.Players)
        {
            if (entry.Value == null) continue;
            if (!_markers.TryGetValue(entry.Key, out var marker) || marker == null)
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(disc.GetComponent<Collider>());
                disc.transform.localScale = new Vector3(1.1f, 0.02f, 1.1f);
                var renderer = disc.GetComponent<Renderer>();
                renderer.sharedMaterial = ArenaProps.Material;
                renderer.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", Paints[PaintIndex(entry.Key)]);
                renderer.SetPropertyBlock(_block);
                _markers[entry.Key] = marker = disc.transform;
            }
            marker.gameObject.SetActive(!entry.Value.IsSpectator);
            marker.position = entry.Value.transform.position + Vector3.up * 0.05f;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        foreach (var marker in _markers.Values) if (marker != null) Destroy(marker.gameObject);
    }
}
