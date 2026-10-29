using UnityEngine;

/// <summary>
/// Applies a look to a character model: one tint per body part (the character mesh has one submesh per part,
/// see <see cref="CosmeticsCatalog.Part"/>) and a hat attached to the head bone. Used by players and the menu preview.
/// </summary>
public class PlayerLook : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Renderer _body;
    [SerializeField] private Transform _head;

    private int _hat = -1;
    private GameObject _hatInstance;
    private MaterialPropertyBlock _block;

    public void Apply(int body, int hands, int head, int hat)
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

        if (hat != _hat && _head != null)
        {
            _hat = hat;
            if (_hatInstance != null)
                DestroyObject(_hatInstance);

            var option = catalog.GetHat(hat);
            if (option.Prefab != null)
            {
                _hatInstance = Instantiate(option.Prefab, _head);
                _hatInstance.transform.localPosition = option.LocalPosition;
                _hatInstance.transform.localRotation = Quaternion.Euler(option.LocalEuler);
                _hatInstance.transform.localScale = Vector3.one * (option.Scale > 0f ? option.Scale : 1f);
                foreach (var c in _hatInstance.GetComponentsInChildren<Collider>(true))
                    DestroyObject(c);
                SetLayerRecursively(_hatInstance, _body != null ? _body.gameObject.layer : gameObject.layer);
            }
        }
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
