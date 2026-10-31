using UnityEngine;

/// <summary>
/// Solid blocks and pillars that minigames add to their arena for a layout (<see cref="MinigameController.Variant"/>).
/// Built locally on every peer from the same layout, so they need no networking.
/// </summary>
public static class ArenaProps
{
    private static MaterialPropertyBlock _block;
    private static Material _material;

    /// <summary>
    /// Lit material for runtime primitives. Unity's default material is not included in builds, so primitives that
    /// keep it render only their shadows there.
    /// </summary>
    public static Material Material => _material != null ? _material : _material = Resources.Load<Material>("ArenaProp");
    private static readonly System.Collections.Generic.HashSet<Collider> _colliders = new();

    /// <summary>Standing on a prop: arenas that count anyone up on their rim as out must not count this.</summary>
    public static bool IsStandingOnProp(Vector3 feet)
    {
        if (_colliders.Count == 0 || !Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        return _colliders.Contains(hit.collider);
    }

    /// <summary>A coloured primitive standing on <paramref name="ground"/> (its base at that point).</summary>
    public static Transform Block(Transform parent, PrimitiveType shape, Vector3 ground, Vector3 size, Color color, float yaw = 0f)
    {
        var go = GameObject.CreatePrimitive(shape);
        go.name = shape + " prop";
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(ground + Vector3.up * size.y * 0.5f, Quaternion.Euler(0f, yaw, 0f));
        // A unit cylinder is two units tall.
        go.transform.localScale = shape == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
        var collider = go.GetComponent<Collider>();
        if (shape == PrimitiveType.Cylinder)
        {
            Object.Destroy(collider);
            var mesh = go.AddComponent<MeshCollider>();
            mesh.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mesh.convex = true;
            collider = mesh;
        }
        _colliders.RemoveWhere(c => c == null);
        _colliders.Add(collider);
        _block ??= new MaterialPropertyBlock();
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = Material;
        renderer.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(_block);
        return go.transform;
    }

    /// <summary>
    /// Dresses <paramref name="block"/> (from <see cref="Block"/>) with a prop-pack model of the same size: the block
    /// keeps its collider and stops rendering. Returns the block for chaining.
    /// </summary>
    public static Transform Skin(Transform block, string model, bool uniform = false, Color? tint = null, float yawOffset = 0f)
    {
        if (block == null) return null;
        var scale = block.lossyScale;
        bool cylinder = block.GetComponent<MeshFilter>() is { sharedMesh: { name: var mesh } } && mesh.StartsWith("Cylinder");
        var size = cylinder ? new Vector3(scale.x, scale.y * 2f, scale.z) : scale;
        var rotation = block.rotation * Quaternion.Euler(0f, yawOffset, 0f);
        if (Mathf.Abs(Mathf.DeltaAngle(yawOffset, 0f)) > 45f && Mathf.Abs(Mathf.DeltaAngle(yawOffset, 180f)) > 45f)
            size = new Vector3(size.z, size.y, size.x);
        var skin = ArenaModels.Spawn(model, block.parent, block.position, rotation, size, uniform, tint);
        if (skin != null && block.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
        return block;
    }

    /// <summary>Layer for <see cref="Fence"/>: it collides with contestants only (set up in <see cref="PhysicsSetup"/>).</summary>
    public const int FENCE_LAYER = 10;
    public const int PLAYER_LAYER = 7;

    /// <summary>
    /// A ring of faint glass wall around an arena: contestants cannot leave it, but thrown bombs and prizes fly through.
    /// </summary>
    public static void Fence(Transform parent, Vector3 centre, float radius, float height = 6f, int segments = 40)
    {
        var root = new GameObject("PlayerFence").transform;
        root.SetParent(parent, true);
        root.position = centre;
        var glass = new Material(Resources.Load<Material>("ThrowArc")) { color = new Color(0.7f, 0.9f, 1f, 0.07f) };
        float width = 2f * Mathf.PI * radius / segments * 1.04f;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i + 0.5f) / segments * Mathf.PI * 2f;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Fence " + i;
            panel.layer = FENCE_LAYER;
            panel.transform.SetParent(root, false);
            panel.transform.SetPositionAndRotation(centre + direction * radius + Vector3.up * height * 0.5f, Quaternion.LookRotation(direction));
            panel.transform.localScale = new Vector3(width, height, 0.3f);
            var renderer = panel.GetComponent<Renderer>();
            renderer.sharedMaterial = glass;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// <summary>Call once after adding props: runtime colliders must reach the physics scene on clients too.</summary>
    public static void Done() => Physics.SyncTransforms();

    public static readonly Color Pink = new(1f, 0.49f, 0.71f), Blue = new(0.36f, 0.75f, 0.92f), Yellow = new(1f, 0.79f, 0.24f),
        Green = new(0.31f, 0.82f, 0.63f), Purple = new(0.72f, 0.61f, 0.95f);
}
