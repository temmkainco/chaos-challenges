using Fusion;
using UnityEngine;

/// <summary>
/// Color Code: a floor of coloured tiles high above the studio. The show calls a colour; when the countdown ends,
/// every other tile drops away. Find a tile of that colour in time (shoving others off it is allowed). The floor
/// comes back reshuffled and every call gets faster. Last contestant standing wins; survivors tie at the bell.
/// Layouts: a square, a cross, four islands (hop the gaps) or a diamond.
/// Chaos: BLACKOUT makes the colours hard to read, ICE FLOOR makes stopping on a tile tricky; its own twist,
/// MIND GAMES, writes the called colour in a different colour (go by the word).
/// </summary>
public class ColorCodeMinigameController : EliminationMinigameController
{
    public const int SIZE = 6;
    private const int TILES = 64;

    public enum CallStage : byte { Shuffle, Calling, Dropped }

    [SerializeField] private Transform _centre;
    [SerializeField] private Material _material;
    [SerializeField] private float _height = 6f;
    [SerializeField] private float _tile = 3.2f;

    private static readonly Color[] Colors = { new(1f, 0.37f, 0.36f), new(1f, 0.79f, 0.24f), new(0.31f, 0.82f, 0.63f), new(0.36f, 0.75f, 0.92f), new(0.72f, 0.61f, 0.95f) };
    private static readonly string[] Names = { "RED", "YELLOW", "GREEN", "BLUE", "PURPLE" };

    [Networked, Capacity(TILES)] private NetworkArray<byte> TileColors { get; }
    [Networked] public byte Target { get; private set; }
    [Networked] public CallStage Stage { get; private set; }
    [Networked] private TickTimer StageTimer { get; set; }
    [Networked] private int Calls { get; set; }
    /// <summary>Colour the call is written in (differs from <see cref="Target"/> during Mind Games).</summary>
    [Networked] private byte Ink { get; set; }

    public override string TwistTitle => "MIND GAMES";
    public override string TwistKind => "DEBUFF";
    public override string TwistDescription => "The called colour is written in the wrong colour. Read the word!";

    private static string Hex(int color) => ColorUtility.ToHtmlStringRGB(Colors[color] * 0.85f);

    private Transform[] _tiles;
    private Collider[] _colliders;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _block;
    private int _shownVersion = -1;

    private Vector3 Top => _centre.position + Vector3.up * _height;
    protected override float OutHeight => Top.y - 3f;

    public override string[] Variants => new[] { "Square", "Cross", "Islands", "Diamond" };

    private readonly System.Collections.Generic.List<Vector3> _positions = new();
    private int Count => _positions.Count;

    private Vector3 TilePosition(int i) => _positions[i];

    /// <summary>Tile centres for this round's layout (same on every peer).</summary>
    private void Layout()
    {
        if (_positions.Count > 0) return;
        void Grid(int size, System.Func<int, int, bool> solid, float spread = 0f)
        {
            float offset = (size - 1) * _tile * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (solid(x, y) && _positions.Count < TILES)
                    {
                        // Islands: push each quarter outwards to leave a hop between them.
                        float pushX = spread * (x < size / 2 ? -1f : 1f), pushZ = spread * (y < size / 2 ? -1f : 1f);
                        _positions.Add(Top + new Vector3(x * _tile - offset + pushX, 0f, y * _tile - offset + pushZ));
                    }
        }
        switch (Variant)
        {
            case 1: Grid(8, (x, y) => (x >= 2 && x <= 5) || (y >= 2 && y <= 5)); break;
            case 2: Grid(6, (x, y) => true, 0.8f); break;
            case 3: Grid(9, (x, y) => Mathf.Abs(x - 4) + Mathf.Abs(y - 4) <= 4); break;
            default: Grid(SIZE, (x, y) => true); break;
        }
    }

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        Layout();
        return TilePosition((slot * 7 + 3) % Count) + Vector3.up * 0.6f;
    }

    public override string Callout => IsGameActive && Stage == CallStage.Calling
        ? $"<color=#{Hex(Ink)}>{Names[Target]}</color>  <size=70%>{Mathf.CeilToInt(StageTimer.RemainingTime(Runner) ?? 0f)}</size>"
        : null;

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        return Stage == CallStage.Calling
            ? $"Stand on <color=#{Hex(Ink)}><b>{Names[Target]}</b></color>! {Mathf.CeilToInt(StageTimer.RemainingTime(Runner) ?? 0f)}"
            : "Get ready for the next call...";
    }

    public override void Spawned()
    {
        base.Spawned();
        Layout();
        _tiles = new Transform[Count];
        _colliders = new Collider[Count];
        _renderers = new Renderer[Count];
        _block = new MaterialPropertyBlock();
        var root = new GameObject("ColorTiles").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < Count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Tile " + i;
            go.transform.SetParent(root, false);
            go.transform.position = TilePosition(i) - Vector3.up * 0.3f;
            go.transform.localScale = new Vector3(_tile * 0.94f, 0.6f, _tile * 0.94f);
            _tiles[i] = go.transform;
            _colliders[i] = go.GetComponent<Collider>();
            _renderers[i] = go.GetComponent<Renderer>();
            _renderers[i].sharedMaterial = _material;
        }
        Physics.SyncTransforms();   // runtime colliders must reach the physics scene on clients too
    }

    protected override void OnIntro()
    {
        base.OnIntro();
        Shuffle();
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        Calls = 0;
        Stage = CallStage.Shuffle;
        StageTimer = TickTimer.CreateFromSeconds(Runner, 2.5f);
    }

    private void Shuffle()
    {
        for (int i = 0; i < Count; i++)
            TileColors.Set(i, (byte)Random.Range(0, Colors.Length));
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive || !StageTimer.Expired(Runner)) return;

        switch (Stage)
        {
            case CallStage.Shuffle:
                Target = (byte)Random.Range(0, Colors.Length);
                Stage = CallStage.Calling;
                // Each call is faster: 4 s down to a near-impossible 1 s.
                StageTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1f, 4f - Calls * 0.45f));
                Ink = TwistActive ? (byte)((Target + Random.Range(1, Colors.Length)) % Colors.Length) : Target;
                break;
            case CallStage.Calling:
                Stage = CallStage.Dropped;
                StageTimer = TickTimer.CreateFromSeconds(Runner, 1.6f);
                Calls++;
                break;
            default:
                Shuffle();
                Stage = CallStage.Shuffle;
                StageTimer = TickTimer.CreateFromSeconds(Runner, 1f);
                break;
        }
        ApplyTiles();
    }

    public override void Render()
    {
        base.Render();
        ApplyTiles();
    }

    /// <summary>Colour every tile; while a call has dropped, only the called colour stays (on every peer).</summary>
    private void ApplyTiles()
    {
        if (_tiles == null || Object == null || !Object.IsValid) return;
        int version = (int)Stage * 1000 + Target * 100 + Calls;
        bool blackout = ActiveChaos == ChaosModifier.Blackout;
        for (int i = 0; i < Count; i++)
        {
            int color = TileColors[i];
            bool gone = IsGameActive && Stage == CallStage.Dropped && color != Target;
            if (_colliders[i].enabled == gone) _colliders[i].enabled = !gone;
            if (_tiles[i].gameObject.activeSelf == gone) _tiles[i].gameObject.SetActive(!gone);
            if (version == _shownVersion && !blackout) continue;
            _renderers[i].GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", blackout ? Color.Lerp(Colors[color], Color.gray, 0.7f) : Colors[color]);
            _renderers[i].SetPropertyBlock(_block);
        }
        if (version != _shownVersion && Stage == CallStage.Dropped)
            Sfx.Play(Sound.Crack, 0.8f, 0.1f);
        _shownVersion = version;
    }
}

