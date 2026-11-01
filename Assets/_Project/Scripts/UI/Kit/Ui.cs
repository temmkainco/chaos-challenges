using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds kit pieces from code (like the rest of the game's UI): canvases set up from the theme, boxes, themed text,
/// icons and buttons. Layout helpers place things by anchor so they keep their corner at any aspect ratio.
/// </summary>
public static class Ui
{
    /// <summary>
    /// A screen-space canvas scaled from the theme's reference resolution (Expand: the 1920x1080 design always fits,
    /// other aspect ratios get extra room) with a raycaster and a safe-area child (<see cref="SafeArea"/>) for content.
    /// </summary>
    public static RectTransform Canvas(string name, int sortingOrder, Transform parent = null)
    {
        var theme = UiTheme.Instance;
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null) go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme.ReferenceResolution;
        scaler.screenMatchMode = theme.ScreenMatch;
        scaler.matchWidthOrHeight = theme.MatchWidthOrHeight;
        var safe = Rect(go.transform, "Safe");
        Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();
        return safe;
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Anchors and pivots at <paramref name="anchor"/> (0..1 in the parent), offset and size in canvas units.</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    public static UiBox Box(Transform parent, string name, UiTheme.Tone fill, float radius = -1f, bool shadow = true)
    {
        var rt = Rect(parent, name);
        var box = rt.gameObject.AddComponent<UiBox>();
        box.Fill = fill;
        box.Radius = radius;
        box.HasShadow = shadow;
        box.Apply();
        return box;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, UiTheme.TextRole role, UiTheme.Tone tone = UiTheme.Tone.Paper,
        TextAlignmentOptions align = TextAlignmentOptions.Center, bool outlined = false)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = align;
        var themed = go.AddComponent<ThemedText>();
        themed.Set(role, tone, outlined);
        return tmp;
    }

    public static Image Icon(Transform parent, string icon, float size, UiTheme.Tone tone)
    {
        var go = new GameObject("Icon " + icon, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = UiShapes.Icon(icon);
        image.color = UiTheme.Instance.Color(tone);
        image.raycastTarget = false;
        image.preserveAspect = true;
        ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
        return image;
    }

    public static Image Circle(Transform parent, string name, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = UiShapes.Circle;
        image.color = color;
        image.raycastTarget = false;
        ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
        return image;
    }

    public static UiButton Button(Transform parent, string label, UiButton.Variant variant = UiButton.Variant.Secondary,
        string icon = null, UnityEngine.Events.UnityAction onClick = null)
    {
        var rt = Rect(parent, "Button " + label);
        var button = rt.gameObject.AddComponent<UiButton>();
        button.Build(label, variant, icon);
        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }

    /// <summary>Lays children of <paramref name="rt"/> out in a column (or row) with a gap from the spacing scale.</summary>
    public static T Stack<T>(RectTransform rt, int gapStep, TextAnchor align = TextAnchor.UpperCenter) where T : HorizontalOrVerticalLayoutGroup
    {
        var layout = rt.gameObject.AddComponent<T>();
        layout.spacing = UiTheme.Instance.Gap(gapStep);
        layout.childAlignment = align;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return layout;
    }

    /// <summary>Gives a layout child a preferred size.</summary>
    public static LayoutElement Size(Component c, float width, float height)
    {
        var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        if (width > 0f) le.preferredWidth = width;
        if (height > 0f) le.preferredHeight = height;
        return le;
    }
}
