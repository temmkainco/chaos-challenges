using UnityEngine;

/// <summary>
/// Applies a look to a character model: one tint per body part (the character mesh has one submesh per part,
/// see <see cref="CosmeticsCatalog.Part"/>) and up to four wearables (hat, face, neck, back) attached to bones.
/// Used by players and the menu preview.
/// </summary>
public class PlayerLook : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private const int SLOTS = 4;

    [SerializeField] private Renderer _body;
    [SerializeField] private Transform _head;

    private readonly int[] _shown = { -1, -1, -1, -1 };
    private readonly GameObject[] _instances = new GameObject[SLOTS];
    private MaterialPropertyBlock _block;
    private Transform _chest;

    /// <summary>Colours the parts (a property block per submesh: no material copies) and puts on the wearables.</summary>
    public void Apply(Color body, Color hands, Color head, int hat, int face = 0, int neck = 0, int back = 0)
    {
        var catalog = CosmeticsCatalog.Instance;
        if (catalog == null)
            return;

        if (_body != null)
        {
            _block ??= new MaterialPropertyBlock();
            Tint(CosmeticsCatalog.Part.Body, body);
            Tint(CosmeticsCatalog.Part.Hands, hands);
            Tint(CosmeticsCatalog.Part.Head, head);
        }

        Wear(catalog, CosmeticsCatalog.Slot.Hat, hat);
        Wear(catalog, CosmeticsCatalog.Slot.Face, face);
        Wear(catalog, CosmeticsCatalog.Slot.Neck, neck);
        Wear(catalog, CosmeticsCatalog.Slot.Back, back);
    }

    private void Wear(CosmeticsCatalog catalog, CosmeticsCatalog.Slot slot, int index)
    {
        int i = (int)slot;
        if (index == _shown[i] || _head == null)
            return;
        _shown[i] = index;
        if (_instances[i] != null)
            Discard(_instances[i]);
        _instances[i] = null;

        var item = catalog.GetItem(slot, index);
        if (item.Prefab == null)
            return;

        var bone = Bone(item.AttachTo);
        var instance = Instantiate(item.Prefab, bone);
        if (item.WorldSpace)
        {
            // Metres in the character's frame from the bone, sized in metres, worked out from the bone's rest pose:
            // using its current (animated) pose put other players' glasses on their scalps and hats off the back.
            var (restRotation, restScale) = Rest(bone);
            var inverse = Quaternion.Inverse(restRotation);
            instance.transform.localPosition = inverse * item.LocalPosition / restScale;
            instance.transform.localRotation = inverse * Quaternion.Euler(item.LocalEuler);
            instance.transform.localScale = Vector3.one * (item.Scale > 0f ? item.Scale : 1f) / restScale;
        }
        else
        {
            instance.transform.localPosition = item.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(item.LocalEuler);
            instance.transform.localScale = Vector3.one * (item.Scale > 0f ? item.Scale : 1f);
        }
        // Wearables come from prop packs with physics: a leftover Rigidbody would drop them, colliders would snag.
        foreach (var rigidbody in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            Discard(rigidbody);
        }
        foreach (var c in instance.GetComponentsInChildren<Collider>(true))
            Discard(c);
        SetLayerRecursively(instance, _body != null ? _body.gameObject.layer : gameObject.layer);
        _instances[i] = instance;
    }

    /// <summary>
    /// Neck and back wearables are placed from the chest (the rigs have no neck bone; the in-game one has no upper
    /// chest either, and falling back to the head put bow ties on the forehead).
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<Transform, (Quaternion rotation, float scale)> _rest = new();

    // Before the animator first poses the rig: the prefab's own pose, the one wearables are authored in.
    private void Awake()
    {
        if (_head == null) return;
        Rest(_head);
        Rest(Bone(CosmeticsCatalog.Attach.Back));
    }

    /// <summary>The bone's rotation in the character's frame and its scale relative to the character, at rest.</summary>
    private (Quaternion rotation, float scale) Rest(Transform bone)
    {
        if (_rest.TryGetValue(bone, out var rest)) return rest;
        rest = (Quaternion.Inverse(transform.rotation) * bone.rotation,
                Mathf.Max(0.0001f, bone.lossyScale.x / Mathf.Max(0.0001f, transform.lossyScale.x)));
        _rest[bone] = rest;
        return rest;
    }

    private Transform Bone(CosmeticsCatalog.Attach attach)
    {
        if (attach == CosmeticsCatalog.Attach.Head) return _head;
        if (_chest == null)
        {
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
                _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                         ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            if (_chest == null) _chest = _head;
        }
        return _chest;
    }

    private void Tint(CosmeticsCatalog.Part part, Color color)
    {
        int index = (int)part;
        if (index >= _body.sharedMaterials.Length)
            return;

        _body.GetPropertyBlock(_block, index);
        _block.SetColor(BaseColor, color);
        _body.SetPropertyBlock(_block, index);
    }

    // Also works in edit mode (previews).
    private static void Discard(Object target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
