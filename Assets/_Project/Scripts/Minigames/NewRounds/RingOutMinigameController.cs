using UnityEngine;

/// <summary>
/// Ring Out: a sumo stage high above the studio floor that shrinks as the clock runs down. Shoves hit much harder
/// here; knock rivals off the edge and stay on yourself. Last contestant standing wins; survivors tie at the bell.
/// Chaos: GIANTS and ICE FLOOR turn it into a skating brawl; its own twist, SHRINK RAY, squeezes the ring for a while.
/// </summary>
public class RingOutMinigameController : EliminationMinigameController
{
    [SerializeField] private Transform _centre;
    [SerializeField] private Material _material;
    [SerializeField] private float _height = 6f;
    [SerializeField] private float _startRadius = 13f;
    [SerializeField] private float _endRadius = 4f;

    private Transform _ring, _rim;

    [Fusion.Networked] private int TwistStartTick { get; set; }

    public override string TwistTitle => "SHRINK RAY";
    public override string TwistDescription => "The ring shrinks to half its size for a few seconds.";

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier == ChaosModifier.Twist) TwistStartTick = Runner.Tick;
    }

    private Vector3 Top => _centre.position + Vector3.up * _height;
    protected override float OutHeight => Top.y - 3f;
    public override float ShoveStrength => 1.7f;

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
        return Top + Vector3.up * 0.5f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _startRadius * 0.6f;
    }

    /// <summary>Radius from the round clock, so every peer shrinks the ring identically.</summary>
    private float Radius
    {
        get
        {
            if (!IsGameActive || !IsTimeLimited) return _startRadius;
            float total = Definition.GameDuration * MatchSettings.LengthMultiplier;
            float t = 1f - Mathf.Clamp01(TimeRemaining / Mathf.Max(1f, total));
            float radius = Mathf.Lerp(_startRadius, _endRadius, Mathf.SmoothStep(0f, 1f, t));
            if (TwistActive)
            {
                // Squeeze in over a second and grow back over the last second (from networked timers, same on every peer).
                float since = (Runner.Tick - TwistStartTick) * Runner.DeltaTime;
                float squeeze = Mathf.SmoothStep(0f, 1f, Mathf.Min(since, ChaosTimeRemaining));
                radius = Mathf.Lerp(radius, Mathf.Max(3f, radius * 0.5f), squeeze);
            }
            return radius;
        }
    }

    public override string GetStatusText() => IsGameActive ? $"{AliveCount} on the ring" : null;

    public override void Spawned()
    {
        base.Spawned();
        _ring = Disc("Ring", new Color(0.72f, 0.61f, 0.95f), 0.6f, 0f);
        _rim = Disc("RingRim", new Color(1f, 0.79f, 0.24f), 0.5f, -0.06f);
        Destroy(_rim.GetComponent<Collider>());
        Apply(_startRadius);
    }

    private Transform Disc(string name, Color color, float thickness, float offset)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.position = Top + Vector3.up * (offset - thickness * 0.5f);
        Destroy(go.GetComponent<Collider>());
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        collider.convex = true;   // a cylinder is convex; convex contacts are steadier for the character controller
        go.transform.localScale = new Vector3(1f, thickness * 0.5f, 1f);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = _material;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
        return go.transform;
    }

    private float _appliedRadius = -1f;

    /// <summary>
    /// Resizes the ring in 5 cm steps. Rescaling a mesh collider every tick made the predicted local contestant sink
    /// into the floor on clients (the collider is rebuilt during resimulation).
    /// </summary>
    private void Apply(float radius)
    {
        if (_ring == null || Mathf.Abs(radius - _appliedRadius) < 0.05f) return;
        _appliedRadius = radius;
        _ring.localScale = new Vector3(radius * 2f, _ring.localScale.y, radius * 2f);
        _rim.localScale = new Vector3(radius * 2f + 0.5f, _rim.localScale.y, radius * 2f + 0.5f);
        Physics.SyncTransforms();
    }

    public override void FixedUpdateNetwork()
    {
        Apply(Radius);
        base.FixedUpdateNetwork();
    }

    public override void Render()
    {
        base.Render();
        Apply(Radius);
    }
}
