using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Customizer colour picker for one body part: a saturation/value square, a hue bar, a hex field and the catalogue's
/// preset swatches. Every change is applied at once (<see cref="LocalLook.SetColor"/>), so the preview and, in a
/// session, the other players see it live; drags are written to disk when they end. Built from code with
/// <see cref="UiKit"/>, over the customizer's rows.
/// </summary>
public class ColorPickerPopup : MonoBehaviour
{
    private const int SV_TEXTURE = 64;

    private CosmeticsCatalog.Part _part;
    private float _hue, _saturation, _value;
    private TMP_Text _title;
    private RawImage _square, _hueBar;
    private RectTransform _squareMarker, _hueMarker;
    private Image _current;
    private TMP_InputField _hex;
    private Texture2D _squareTexture, _hueTexture;

    public static ColorPickerPopup Create(RectTransform parent, Rect area)
    {
        var go = new GameObject("ColorPicker", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var picker = go.AddComponent<ColorPickerPopup>();
        picker.Build(area);
        go.SetActive(false);
        return picker;
    }

    public void Open(CosmeticsCatalog.Part part)
    {
        _part = part;
        _title.text = part.ToString().ToUpperInvariant() + " COLOUR";
        Color.RGBToHSV(LocalLook.GetColor(part), out _hue, out _saturation, out _value);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        Show(false);
    }

    public void Close()
    {
        if (gameObject.activeSelf) PlayerPrefs.Save();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_squareTexture != null) Destroy(_squareTexture);
        if (_hueTexture != null) Destroy(_hueTexture);
    }

    #region Changes

    private void SetHsv(float hue, float saturation, float value, bool save)
    {
        bool hueChanged = !Mathf.Approximately(hue, _hue);
        _hue = hue; _saturation = saturation; _value = value;
        LocalLook.SetColor(_part, Color.HSVToRGB(_hue, _saturation, _value), save);
        Show(hueChanged);
    }

    private void SetColor(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        // A grey has no hue: keep the bar where it was.
        if (s < 0.001f || v < 0.001f) h = _hue;
        _hue = h; _saturation = s; _value = v;
        LocalLook.SetColor(_part, color);
        Show(true);
    }

    /// <summary>Markers, the current swatch, the square's colours and (unless it's being typed in) the hex field.</summary>
    private void Show(bool redrawSquare)
    {
        if (redrawSquare || _squareTexture.GetPixel(SV_TEXTURE - 1, SV_TEXTURE - 1) != Color.HSVToRGB(_hue, 1f, 1f))
            DrawSquare();
        var size = _square.rectTransform.rect.size;
        _squareMarker.anchoredPosition = new Vector2((_saturation - 0.5f) * size.x, (_value - 0.5f) * size.y);
        _hueMarker.anchoredPosition = new Vector2(0f, (_hue - 0.5f) * _hueBar.rectTransform.rect.height);
        var color = Color.HSVToRGB(_hue, _saturation, _value);
        _current.color = color;
        if (!_hex.isFocused) _hex.SetTextWithoutNotify("#" + LocalLook.ToHex(color));
    }

    private void DrawSquare()
    {
        var pixels = new Color[SV_TEXTURE * SV_TEXTURE];
        for (int y = 0; y < SV_TEXTURE; y++)
            for (int x = 0; x < SV_TEXTURE; x++)
                pixels[y * SV_TEXTURE + x] = Color.HSVToRGB(_hue, x / (SV_TEXTURE - 1f), y / (SV_TEXTURE - 1f));
        _squareTexture.SetPixels(pixels);
        _squareTexture.Apply(false);
    }

    #endregion

    #region Layout

    private void Build(Rect area)
    {
        var kit = UiKit.Instance;
        var root = (RectTransform)transform;
        Place(root, area.center.x, area.center.y, area.width, area.height);
        var card = MakeImage(transform, "Card", kit.Panel, UiKit.Cream);
        card.type = Image.Type.Sliced;
        card.raycastTarget = true;   // the rows underneath don't take clicks while it's open
        Stretch(card.rectTransform);

        float w = area.width, h = area.height, pad = 20f;
        _title = MakeText(transform, "Title", 34f, UiKit.Orange, true, 0f, h / 2f - 34f, w - 2f * pad, 48f);

        float cell = Mathf.Min((w - 2f * pad) / 10f, 52f);
        float top = h / 2f - 70f;
        float squareSize = Mathf.Min(h - 70f - 2f * cell - pad - 24f, w * 0.45f);
        float left = -w / 2f + pad;

        // Saturation (across) / value (up) square for the current hue.
        _squareTexture = new Texture2D(SV_TEXTURE, SV_TEXTURE, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        _square = MakeRaw(transform, "SaturationValue", _squareTexture, left + squareSize / 2f, top - squareSize / 2f, squareSize, squareSize);
        Drag(_square.rectTransform, (p, save) => SetHsv(_hue, p.x, p.y, save));
        _squareMarker = Marker(_square.transform, 26f);

        // Hue, bottom to top.
        _hueTexture = new Texture2D(1, 128, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++) _hueTexture.SetPixel(0, y, Color.HSVToRGB(y / 127f, 1f, 1f));
        _hueTexture.Apply(false);
        float hueX = left + squareSize + 16f + 18f;
        _hueBar = MakeRaw(transform, "Hue", _hueTexture, hueX, top - squareSize / 2f, 36f, squareSize);
        Drag(_hueBar.rectTransform, (p, save) => SetHsv(Mathf.Clamp(p.y, 0f, 0.999f), _saturation, _value, save));
        _hueMarker = (RectTransform)MakeImage(_hueBar.transform, "Marker", kit.Button, Color.white).transform;
        Place(_hueMarker, 0f, 0f, 48f, 12f);
        var hueOutline = _hueMarker.gameObject.AddComponent<UnityEngine.UI.Outline>();
        hueOutline.effectColor = UiKit.Ink; hueOutline.effectDistance = new Vector2(2f, -2f);

        // Right column: the colour, its hex code, OK.
        float columnLeft = hueX + 18f + 24f, columnWidth = w / 2f - pad - columnLeft, columnX = columnLeft + columnWidth / 2f;
        var ring = MakeImage(transform, "CurrentRing", kit.Circle, UiKit.Ink);
        Place(ring.rectTransform, columnX, top - 50f, 100f, 100f);
        _current = MakeImage(transform, "Current", kit.Circle, Color.white);
        Place(_current.rectTransform, columnX, top - 50f, 88f, 88f);
        MakeText(transform, "HexLabel", 24f, UiKit.Ink, false, columnX, top - 124f, columnWidth, 32f).text = "HEX";
        _hex = MakeInput(transform, columnX, top - 166f, Mathf.Min(columnWidth, 190f), 50f);
        _hex.onValueChanged.AddListener(text =>
        {
            if (_hex.isFocused && text.TrimStart('#').Length == 6 && LocalLook.TryParseHex(text, out var color)) SetColor(color);
        });
        _hex.onEndEdit.AddListener(text =>
        {
            if (LocalLook.TryParseHex(text, out var color)) SetColor(color);
            else Show(false);   // not a colour: back to the current one
        });
        var ok = MakeButton(transform, "OK", UiKit.Mint, columnX, top - squareSize + 28f, Mathf.Min(columnWidth, 170f), 56f);
        ok.onClick.AddListener(Close);

        // Presets, two rows of ten.
        var presets = CosmeticsCatalog.Instance != null ? CosmeticsCatalog.Instance.Colors : Array.Empty<CosmeticsCatalog.Swatch>();
        int perRow = Mathf.Max(1, Mathf.CeilToInt(presets.Length / 2f));
        float presetsTop = top - squareSize - 24f - cell / 2f;
        for (int i = 0; i < presets.Length; i++)
        {
            float x = (i % perRow - (perRow - 1) * 0.5f) * cell, y = presetsTop - (i / perRow) * cell;
            var outline = MakeImage(transform, "PresetRing " + presets[i].Name, kit.Circle, UiKit.Ink);
            Place(outline.rectTransform, x, y, cell * 0.86f, cell * 0.86f);
            var swatch = MakeImage(outline.transform, "Preset", kit.Circle, presets[i].Color);
            Place(swatch.rectTransform, 0f, 0f, cell * 0.74f, cell * 0.74f);
            swatch.raycastTarget = true;
            var color = presets[i].Color;
            swatch.gameObject.AddComponent<Button>().onClick.AddListener(() => SetColor(color));
        }
    }

    /// <summary>Pointer down and drag report the point inside <paramref name="rect"/> (0..1); release saves.</summary>
    private static void Drag(RectTransform rect, Action<Vector2, bool> changed)
    {
        rect.GetComponent<Graphic>().raycastTarget = true;
        var area = rect.gameObject.AddComponent<DragArea>();
        area.Changed = changed;
    }

    private class DragArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<Vector2, bool> Changed;

        public void OnPointerDown(PointerEventData e) => Report(e, false);
        public void OnDrag(PointerEventData e) => Report(e, false);
        public void OnPointerUp(PointerEventData e) => Report(e, true);

        private void Report(PointerEventData e, bool save)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local)) return;
            var r = rect.rect;
            Changed?.Invoke(new Vector2(Mathf.Clamp01((local.x - r.xMin) / r.width), Mathf.Clamp01((local.y - r.yMin) / r.height)), save);
        }
    }

    private static RectTransform Marker(Transform parent, float size)
    {
        var kit = UiKit.Instance;
        var outer = MakeImage(parent, "Marker", kit.Circle, UiKit.Ink);
        Place(outer.rectTransform, 0f, 0f, size, size);
        var inner = MakeImage(outer.transform, "Inner", kit.Circle, Color.white);
        Place(inner.rectTransform, 0f, 0f, size - 8f, size - 8f);
        return outer.rectTransform;
    }

    private static TMP_InputField MakeInput(Transform parent, float x, float y, float w, float h)
    {
        var kit = UiKit.Instance;
        var go = new GameObject("Hex", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.SetActive(false);   // TMP_InputField wants its parts before it is enabled
        go.transform.SetParent(parent, false);
        var background = go.GetComponent<Image>();
        background.sprite = kit.Button; background.type = Image.Type.Sliced; background.color = Color.white;
        Place((RectTransform)go.transform, x, y, w, h);

        var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(go.transform, false);
        Stretch(viewport, 10f);
        var text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(viewport, false);
        Stretch(text.rectTransform);
        text.font = kit.Font; text.fontSize = 28f; text.color = UiKit.Ink; text.alignment = TextAlignmentOptions.Center;

        var input = go.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.fontAsset = kit.Font;
        input.pointSize = 28f;
        input.characterLimit = 7;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.targetGraphic = background;
        go.SetActive(true);
        return input;
    }

    private static Button MakeButton(Transform parent, string label, Color color, float x, float y, float w, float h)
    {
        var kit = UiKit.Instance;
        var image = MakeImage(parent, label, kit.Button, color);
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;
        Place(image.rectTransform, x, y, w, h);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        MakeText(image.transform, "Label", 30f, UiKit.Ink, false, 0f, 3f, w, h).text = label;
        return button;
    }

    private static RawImage MakeRaw(Transform parent, string name, Texture texture, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var raw = go.GetComponent<RawImage>();
        raw.texture = texture;
        Place(raw.rectTransform, x, y, w, h);
        return raw;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text MakeText(Transform parent, string name, float size, Color color, bool outline, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        var kit = UiKit.Instance;
        text.font = kit.Font;
        if (outline && kit.OutlineThick != null) text.fontSharedMaterial = kit.OutlineThick;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Place(text.rectTransform, x, y, w, h);
        return text;
    }

    #endregion
}
