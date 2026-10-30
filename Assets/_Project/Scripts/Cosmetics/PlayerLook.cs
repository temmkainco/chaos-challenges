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
    private Transform _neck, _chest;

    public void Apply(int body, int hands, int head, int hat, int face = 0, int neck = 0, int back = 0)
    {
        var catalog = CosmeticsCatalog.Instance;
        if (catalog == null)
            return;

        if (_body != null)
        {
            _block ??= new MaterialPropertyBlock();
            Tint(CosmeticsCatalog.Part.Body, catalog.GetColor(body).Color);
            Tint(CosmeticsCatalog.Part.Hands, catalog.GetColor(hands).Color);
            Tint(CosmeticsCatalog.Part.Head, catalog.GetColor(head).Color);
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
            DestroyObject(_instances[i]);
        _instances[i] = null;

        var item = catalog.GetItem(slot, index);
        if (item.Prefab == null)
            return;

        var bone = Bone(item.AttachTo);
        var instance = Instantiate(item.Prefab, bone);
        if (item.WorldSpace)
        {
            // Metres in the character's frame from the bone, sized in metres: independent of the rig's scale.
            var frame = transform.rotation;
            instance.transform.position = bone.position + frame * item.LocalPosition;
            instance.transform.rotation = frame * Quaternion.Euler(item.LocalEuler);
            float boneScale = Mathf.Max(0.0001f, bone.lossyScale.x);
            instance.transform.localScale = Vector3.one * (item.Scale > 0f ? item.Scale : 1f) / boneScale;
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
            DestroyObject(rigidbody);
        }
        foreach (var c in instance.GetComponentsInChildren<Collider>(true))
            DestroyObject(c);
        SetLayerRecursively(instance, _body != null ? _body.gameObject.layer : gameObject.layer);
        _instances[i] = instance;
    }

    private Transform Bone(CosmeticsCatalog.Attach attach)
    {
        if (attach == CosmeticsCatalog.Attach.Head) return _head;
        if (_neck == null || _chest == null)
        {
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                _neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? _head;
                _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                         ?? animator.GetBoneTransform(HumanBodyBones.Spine) ?? _head;
            }
            _neck ??= _head;
            _chest ??= _head;
        }
        return attach == CosmeticsCatalog.Attach.Neck ? _neck : _chest;
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
    private static void DestroyObject(Object target)
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
