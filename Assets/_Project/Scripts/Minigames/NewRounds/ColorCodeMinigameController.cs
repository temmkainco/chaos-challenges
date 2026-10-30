using Fusion;
using UnityEngine;

/// <summary>
/// Color Code: a floor of coloured tiles high above the studio. The show calls a colour; when the countdown ends,
/// every other tile drops away. Find a tile of that colour in time (shoving others off it is allowed). The floor
/// comes back reshuffled and every call gets faster. Last contestant standing wins; survivors tie at the bell.
/// Chaos: BLACKOUT makes the colours hard to read, ICE FLOOR makes stopping on a tile tricky; its own twist,
/// MIND GAMES, writes the called colour in a different colour (go by the word).
/// </summary>
public class ColorCodeMinigameController : EliminationMinigameController
{
    public const int SIZE = 6;
    private const int TILES = SIZE * SIZE;

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

    private Vector3 TilePosition(int i)
    {
        float offset = (SIZE - 1) * _tile * 0.5f;
        return Top + new Vector3((i % SIZE) * _tile - offset, 0f, (i / SIZE) * _tile - offset);
    }

    protected override Vector3 ArenaSpawn(int slot, int count) =>
        TilePosition((slot * 7 + 3) % TILES) + Vector3.up * 0.6f;

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
        _tiles = new Transform[TILES];
        _colliders = new Collider[TILES];
        _renderers = new Renderer[TILES];
        _block = new MaterialPropertyBlock();
        var root = new GameObject("ColorTiles").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < TILES; i++)
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
        for (int i = 0; i < TILES; i++)
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
                // Each call is faster: 5 s down to 2 s.
                StageTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(2f, 5f - Calls * 0.4f));
                Ink = TwistActive ? (byte)((Target + Random.Range(1, Colors.Length)) % Colors.Length) : Target;
                Announce(Ink != Target ? $"<color=#{Hex(Ink)}>{Names[Target]}</color>!" : $"{Names[Target]}!");
                break;
            case CallStage.Calling:
                Stage = CallStage.Dropped;
                StageTimer = TickTimer.CreateFromSeconds(Runner, 2.2f);
                Calls++;
                break;
            default:
                Shuffle();
                Stage = CallStage.Shuffle;
                StageTimer = TickTimer.CreateFromSeconds(Runner, 1.5f);
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
        for (int i = 0; i < TILES; i++)
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

