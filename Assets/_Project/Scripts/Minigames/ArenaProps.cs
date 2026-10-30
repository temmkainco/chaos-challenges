using UnityEngine;

/// <summary>
/// Solid blocks and pillars that minigames add to their arena for a layout (<see cref="MinigameController.Variant"/>).
/// Built locally on every peer from the same layout, so they need no networking.
/// </summary>
public static class ArenaProps
{
    private static MaterialPropertyBlock _block;
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
        renderer.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(_block);
        return go.transform;
    }

    /// <summary>Call once after adding props: runtime colliders must reach the physics scene on clients too.</summary>
    public static void Done() => Physics.SyncTransforms();

    public static readonly Color Pink = new(1f, 0.49f, 0.71f), Blue = new(0.36f, 0.75f, 0.92f), Yellow = new(1f, 0.79f, 0.24f),
        Green = new(0.31f, 0.82f, 0.63f), Purple = new(0.72f, 0.61f, 0.95f);
}
