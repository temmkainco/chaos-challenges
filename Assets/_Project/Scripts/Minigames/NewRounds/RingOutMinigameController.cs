using UnityEngine;

/// <summary>
/// Ring Out: a sumo stage high above the studio floor that shrinks as the clock runs down. Shoves hit much harder
/// here; knock rivals off the edge and stay on yourself. Last contestant standing wins; survivors tie at the bell.
/// Chaos twist: GIANTS and ICE FLOOR turn it into a skating brawl.
/// </summary>
public class RingOutMinigameController : EliminationMinigameController
{
    [SerializeField] private Transform _centre;
    [SerializeField] private Material _material;
    [SerializeField] private float _height = 6f;
    [SerializeField] private float _startRadius = 13f;
    [SerializeField] private float _endRadius = 4f;

    private Transform _ring, _rim;

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
            return Mathf.Lerp(_startRadius, _endRadius, Mathf.SmoothStep(0f, 1f, t));
        }
    }

    public override string GetStatusText() => IsGameActive ? $"{_alive.Count} on the ring" : null;

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
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        go.transform.localScale = new Vector3(1f, thickness * 0.5f, 1f);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = _material;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
        return go.transform;
    }

    private void Apply(float radius)
    {
        if (_ring == null) return;
        _ring.localScale = new Vector3(radius * 2f, _ring.localScale.y, radius * 2f);
        _rim.localScale = new Vector3(radius * 2f + 0.5f, _rim.localScale.y, radius * 2f + 0.5f);
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
