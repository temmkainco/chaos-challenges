using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prop-pack models used to dress the code-built arenas (Resources/ArenaModels). <see cref="ArenaProps.Skin"/> puts one
/// over a collision block: gameplay keeps the exact box collider, players see the model.
/// </summary>
[CreateAssetMenu(menuName = "Chaos/Arena Models")]
public class ArenaModels : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string Key;
        public GameObject Prefab;
    }

    [SerializeField] private List<Entry> _entries = new();

    private static ArenaModels _instance;
    private Dictionary<string, GameObject> _byKey;

    public static ArenaModels Instance => _instance != null ? _instance : _instance = Resources.Load<ArenaModels>("ArenaModels");

    public GameObject Get(string key)
    {
        if (_byKey == null)
        {
            _byKey = new Dictionary<string, GameObject>();
            foreach (var entry in _entries)
                if (entry.Prefab != null) _byKey[entry.Key] = entry.Prefab;
        }
        return _byKey.TryGetValue(key, out var prefab) ? prefab : null;
    }

    /// <summary>
    /// A copy of the model fitted into a box of <paramref name="size"/> (metres, in <paramref name="rotation"/>'s axes)
    /// centred on <paramref name="centre"/>. Uniform keeps the model's proportions (fits the smallest side). Visual only:
    /// colliders and rigidbodies are stripped.
    /// </summary>
    public static Transform Spawn(string key, Transform parent, Vector3 centre, Quaternion rotation, Vector3 size, bool uniform = false, Color? tint = null)
    {
        var prefab = Instance != null ? Instance.Get(key) : null;
        if (prefab == null) return null;
        // Measured and sized unparented at the origin, then attached (a scaled parent can't skew the measurement).
        var holder = new GameObject(key).transform;
        var model = Instantiate(prefab, holder);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) Destroy(body);
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { Destroy(holder.gameObject); return null; }
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var scale = new Vector3(size.x / Mathf.Max(0.001f, bounds.size.x), size.y / Mathf.Max(0.001f, bounds.size.y), size.z / Mathf.Max(0.001f, bounds.size.z));
        if (uniform) scale = Vector3.one * Mathf.Min(scale.x, scale.y, scale.z);
        model.transform.localPosition = -bounds.center;
        holder.localScale = scale;
        holder.SetPositionAndRotation(centre, rotation);
        if (parent != null) holder.SetParent(parent, true);

        if (tint.HasValue)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint.Value);
                renderer.SetPropertyBlock(block);
            }
        }
        return holder;
    }
}
