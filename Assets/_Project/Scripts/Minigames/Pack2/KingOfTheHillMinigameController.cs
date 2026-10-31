using Fusion;
using UnityEngine;

/// <summary>
/// King of the Hill: a glowing zone sits somewhere on the stage and jumps to a new spot every few seconds. Stand in
/// it alone to score; with two or more inside it is contested and nobody scores, so shove! Most points at the bell
/// wins. Layouts: Open Stage, Pillars, Shrinking Zone. Twist: DOUBLE POINTS.
/// </summary>
public class KingOfTheHillMinigameController : ScoreMinigameController
{
    private const float ZONE_RADIUS = 3.2f, ARENA_RADIUS = 10f, MOVE_SECONDS = 9f;

    [Networked] private Vector3 Zone { get; set; }
    [Networked] private TickTimer NextMove { get; set; }
    [Networked] private PlayerRef King { get; set; }
    [Networked] private NetworkBool Contested { get; set; }

    public override string[] Variants => new[] { "Open Stage", "Pillars", "Shrinking Zone" };
    public override string TwistTitle => "DOUBLE POINTS";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "Points in the zone count double.";
    public override float ShoveStrength => 1.4f;

    private Transform _ring, _beam;
    private Renderer _ringRenderer, _beamRenderer;
    private Material _beamMaterial;
    private MaterialPropertyBlock _block;

    private float Radius
    {
        get
        {
            if (Variant != 2 || !IsGameActive) return ZONE_RADIUS;
            float total = Definition.GameDuration * MatchSettings.LengthMultiplier;
            return Mathf.Lerp(ZONE_RADIUS + 0.6f, 1.8f, Mathf.Clamp01(1f - TimeRemaining / total));
        }
    }

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
        return ArenaCentre + Vector3.up * 0.5f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ARENA_RADIUS * 0.8f;
    }

    public override void Spawned()
    {
        base.Spawned();
        if (HasStateAuthority) Zone = ArenaCentre;
        ArenaProps.Fence(transform, ArenaCentre, ARENA_RADIUS + 1.5f);
        if (Variant == 1)
            for (int i = 0; i < 5; i++)
            {
                float angle = i / 5f * Mathf.PI * 2f + 0.3f;
                ArenaProps.Block(transform, PrimitiveType.Cylinder, ArenaCentre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 5.5f,
                    new Vector3(1.4f, 2.6f, 1.4f), i % 2 == 0 ? ArenaProps.Pink : ArenaProps.Blue);
            }
        ArenaProps.Done();

        _block = new MaterialPropertyBlock();
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(transform, false);
        _ring = ring.transform;
        _ringRenderer = ring.GetComponent<Renderer>();
        _ringRenderer.sharedMaterial = ArenaProps.Material;
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(beam.GetComponent<Collider>());
        beam.transform.SetParent(transform, false);
        _beam = beam.transform;
        _beamRenderer = beam.GetComponent<Renderer>();
        _beamMaterial = new Material(Resources.Load<Material>("ThrowArc"));
        _beamRenderer.sharedMaterial = _beamMaterial;
        _beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        Zone = ArenaCentre;
        NextMove = TickTimer.CreateFromSeconds(Runner, MOVE_SECONDS);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;

        if (NextMove.Expired(Runner))
        {
            NextMove = TickTimer.CreateFromSeconds(Runner, MOVE_SECONDS);
            for (int guard = 0; guard < 20; guard++)
            {
                var spot = ArenaCentre + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (ARENA_RADIUS - ZONE_RADIUS);
                if ((spot - ArenaCentre).magnitude > ARENA_RADIUS - ZONE_RADIUS || (spot - Zone).magnitude < 5f) continue;
                Zone = spot;
                break;
            }
            Announce("THE HILL MOVED!");
        }

        // Four times a second: one contestant alone inside scores.
        if (Runner.Tick % Mathf.Max(1, Runner.TickRate / 4) != 0) return;
        PlayerRef inside = PlayerRef.None;
        int count = 0;
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || player.IsSpectator || player.ControlsLocked) continue;
            var rel = player.transform.position - Zone;
            if (new Vector2(rel.x, rel.z).magnitude > Radius || Mathf.Abs(rel.y) > 2.5f) continue;
            inside = entry.Key;
            count++;
        }
        Contested = count > 1;
        King = count == 1 ? inside : PlayerRef.None;
        if (count == 1) AddScore(inside, TwistActive ? 2 : 1);
    }

    public override string Callout =>
        !IsGameActive ? null :
        Contested ? "<color=#E03131>CONTESTED!</color>" :
        King != PlayerRef.None && King == Runner.LocalPlayer ? "<color=#2F9E44>YOU'RE THE KING!</color>" : null;

    public override void Render()
    {
        base.Render();
        if (_ring == null) return;
        float radius = Radius;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
        _ring.position = Zone + Vector3.up * 0.06f;
        _ring.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
        var color = Contested ? new Color(1f, 0.37f, 0.36f) : King != PlayerRef.None ? new Color(0.31f, 0.82f, 0.63f) : new Color(1f, 0.79f, 0.24f);
        _block.SetColor("_BaseColor", Color.Lerp(color, Color.white, pulse * 0.3f));
        _ringRenderer.SetPropertyBlock(_block);
        _beam.position = Zone + Vector3.up * 6f;
        _beam.localScale = new Vector3(radius * 2f, 6f, radius * 2f);
        _beamMaterial.color = new Color(color.r, color.g, color.b, 0.12f + 0.06f * pulse);
        bool show = IsGameActive || Phase == MinigamePhase.Countdown;
        if (_ring.gameObject.activeSelf != show) { _ring.gameObject.SetActive(show); _beam.gameObject.SetActive(show); }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (_beamMaterial != null) Destroy(_beamMaterial);
    }
}
