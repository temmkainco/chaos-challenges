using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The kit's box: a hard Ink drop shadow, an Ink outline and a rounded fill in a palette tone, sized from the theme
/// (corner radius, outline width, shadow depth). Panels, buttons, chips and cards are all boxes. Content goes in as
/// children after the three layers.
/// </summary>
[DisallowMultipleComponent]
public class UiBox : MonoBehaviour
{
    public UiTheme.Tone Fill = UiTheme.Tone.Studio;
    [Tooltip("Corner radius in canvas units; negative = the theme's panel radius")] public float Radius = -1f;
    [Tooltip("Shadow depth; negative = the theme's")] public float ShadowDepth = -1f;
    public bool HasShadow = true;
    public bool HasOutline = true;
    [Tooltip("Fill opacity (scrims, glass)")] [Range(0f, 1f)] public float FillAlpha = 1f;

    private Image _shadow, _outline, _fill;
    private float _shadowNow = -1f;

    public Image FillImage { get { Ensure(); return _fill; } }

    private void Awake() => Ensure();
    private void OnEnable() { UiTheme.Changed += Apply; Apply(); }
    private void OnDisable() => UiTheme.Changed -= Apply;

    private void Ensure()
    {
        if (_fill != null) return;
        _shadow = Layer("Shadow", 0);
        _outline = Layer("Outline", 1);
        _fill = Layer("Fill", 2);
    }

    private Image Layer(string name, int index)
    {
        var existing = transform.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(index);
        var image = go.GetComponent<Image>();
        image.raycastTarget = index == 2;
        image.sprite = UiShapes.RoundRect;
        image.type = Image.Type.Sliced;
        // Layers fill the box; a layout group on the box (a modal's column) must not stack them.
        (go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>()).ignoreLayout = true;
        return image;
    }

    /// <summary>Current shadow depth (buttons sink into it when pressed).</summary>
    public void SetShadow(float depth)
    {
        _shadowNow = depth;
        Apply();
    }

    public void SetFill(UiTheme.Tone tone)
    {
        Fill = tone;
        Apply();
    }

    public void Apply()
    {
        Ensure();
        var theme = UiTheme.Instance;
        float radius = Radius >= 0f ? Radius : theme.RadiusPanel;
        float outline = HasOutline ? theme.Outline : 0f;
        float shadow = !HasShadow ? 0f : _shadowNow >= 0f ? _shadowNow : ShadowDepth >= 0f ? ShadowDepth : theme.Shadow;

        Stretch(_shadow.rectTransform, 0f, -shadow);
        _shadow.color = theme.Ink;
        _shadow.enabled = shadow > 0.01f;
        _shadow.pixelsPerUnitMultiplier = UiShapes.RadiusMultiplier(radius);

        Stretch(_outline.rectTransform, 0f, 0f);
        _outline.color = theme.Ink;
        _outline.enabled = outline > 0f;
        _outline.pixelsPerUnitMultiplier = UiShapes.RadiusMultiplier(radius);

        Stretch(_fill.rectTransform, outline, 0f);
        var c = theme.Color(Fill);
        c.a *= FillAlpha;
        _fill.color = c;
        _fill.pixelsPerUnitMultiplier = UiShapes.RadiusMultiplier(Mathf.Max(1f, radius - outline));
    }

    private static void Stretch(RectTransform rt, float inset, float dy)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset + dy);
        rt.offsetMax = new Vector2(-inset, -inset + dy);
    }
}
