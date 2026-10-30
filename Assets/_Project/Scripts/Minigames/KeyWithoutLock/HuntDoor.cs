using DG.Tweening;
using UnityEngine;

/// <summary>
/// One door in Key Hunt. Local visuals only (its colour while open, swing open for an escape, rattle for a wrong key); the
/// minigame controller decides which keys fit. The door faces into the room along its forward axis.
/// </summary>
public class HuntDoor : MonoBehaviour
{
    [SerializeField] private int _id;
    [SerializeField, Tooltip("Rotates when the door opens")] private Transform _hinge;

    public int Id => _id;
    /// <summary>Where a contestant stands to try a key.</summary>
    public Vector3 FrontPosition => transform.position + transform.forward * 1.4f;

    public void Configure(int id, Transform hinge)
    {
        _id = id;
        _hinge = hinge;
    }

    private Transform _lamp;
    private Material _lampMaterial;
    private MaterialPropertyBlock _block;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    /// <summary>Open in a key colour (the panel takes the colour and a lamp glows above), or shut (dark, no lamp).</summary>
    public void ShowState(Color? color)
    {
        _block ??= new MaterialPropertyBlock();
        var panelColor = color.HasValue ? Color.Lerp(color.Value, Color.white, 0.15f) : new Color(0.22f, 0.2f, 0.26f);
        var target = _hinge != null ? _hinge : transform;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, panelColor);
            renderer.SetPropertyBlock(_block);
        }

        if (_lamp == null)
        {
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(lamp.GetComponent<Collider>());
            lamp.name = "DoorLamp";
            _lamp = lamp.transform;
            _lamp.SetParent(transform, false);
            _lamp.position = transform.position + Vector3.up * 3.3f + transform.forward * 0.3f;
            _lamp.localScale = Vector3.one * 0.7f;
            _lampMaterial = new Material(Resources.Load<Material>("ThrowArc"));
            var lampRenderer = lamp.GetComponent<Renderer>();
            lampRenderer.sharedMaterial = _lampMaterial;
            lampRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        _lamp.gameObject.SetActive(color.HasValue);
        if (color.HasValue)
        {
            _lampMaterial.color = color.Value;
            _lamp.DOKill(true);
            _lamp.DOScale(0.85f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(_lamp.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (_lampMaterial != null) Destroy(_lampMaterial);
    }

    public void PlayOpen()
    {
        if (_hinge == null) return;
        _hinge.DOKill(true);
        DOTween.Sequence()
            .Append(_hinge.DOLocalRotate(new Vector3(0f, -100f, 0f), 0.35f).SetEase(Ease.OutBack))
            .AppendInterval(1.2f)
            .Append(_hinge.DOLocalRotate(Vector3.zero, 0.4f).SetEase(Ease.InQuad))
            .SetLink(gameObject);
    }

    public void PlayReject()
    {
        if (_hinge == null) return;
        _hinge.DOKill(true);
        _hinge.DOShakeRotation(0.4f, new Vector3(0f, 8f, 0f), 18).SetLink(gameObject);
    }
}
