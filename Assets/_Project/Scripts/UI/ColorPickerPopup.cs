using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Customizer colour picker for one body part: a saturation/value square, a hue bar, a hex field and the catalogue's
/// preset swatches. Every change is applied at once (<see cref="LocalLook.SetColor"/>), so the preview and, in a
/// session, the other players see it live; drags are written to disk when they end. Built from code with
/// the UI kit (<see cref="UiTheme"/>), over the customizer's rows.
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
        _title.text = part + " colour";
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
        var theme = UiTheme.Instance;
        var root = (RectTransform)transform;
        Place(root, area.center.x, area.center.y, area.width, area.height);
        // A kit panel; its fill takes clicks, so the rows underneath don't while it's open.
        var card = Ui.Box(transform, "Card", UiTheme.Tone.Studio);
        Ui.Stretch((RectTransform)card.transform);

        float w = area.width, h = area.height, pad = 24f;
        _title = Ui.Text(transform, "", UiTheme.TextRole.Title, UiTheme.Tone.Paper);
        Place(_title.rectTransform, 0f, h / 2f - 40f, w - 2f * pad, 52f);

        float cell = Mathf.Min((w - 2f * pad) / 10f, 54f);
        float top = h / 2f - 80f;
        float squareSize = Mathf.Min(h - 70f - 2f * cell - pad - 24f, w * 0.45f);
        float left = -w / 2f + pad;

        // Saturation (across) / value (up) square for the current hue.
        _squareTexture = new Texture2D(SV_TEXTURE, SV_TEXTURE, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        Frame(left + squareSize / 2f, top - squareSize / 2f, squareSize, squareSize);
        _square = MakeRaw(transform, "SaturationValue", _squareTexture, left + squareSize / 2f, top - squareSize / 2f, squareSize, squareSize);
        Drag(_square.rectTransform, (p, save) => SetHsv(_hue, p.x, p.y, save));
        _squareMarker = Marker(_square.transform, 26f);

        // Hue, bottom to top.
        _hueTexture = new Texture2D(1, 128, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++) _hueTexture.SetPixel(0, y, Color.HSVToRGB(y / 127f, 1f, 1f));
        _hueTexture.Apply(false);
        float hueX = left + squareSize + 16f + 18f;
        Frame(hueX, top - squareSize / 2f, 36f, squareSize);
        _hueBar = MakeRaw(transform, "Hue", _hueTexture, hueX, top - squareSize / 2f, 36f, squareSize);
        Drag(_hueBar.rectTransform, (p, save) => SetHsv(Mathf.Clamp(p.y, 0f, 0.999f), _saturation, _value, save));
        var hueMarker = Ui.Box(_hueBar.transform, "Marker", UiTheme.Tone.Paper, 6f, false);
        _hueMarker = (RectTransform)hueMarker.transform;
        Place(_hueMarker, 0f, 0f, 52f, 18f);

        // Right column: the colour, its hex code, OK.
        float columnLeft = hueX + 18f + 24f, columnWidth = w / 2f - pad - columnLeft, columnX = columnLeft + columnWidth / 2f;
        var ring = Ui.Circle(transform, "CurrentRing", 104f, theme.Ink);
        Place(ring.rectTransform, columnX, top - 52f, 104f, 104f);
        _current = Ui.Circle(transform, "Current", 90f, Color.white);
        Place(_current.rectTransform, columnX, top - 52f, 90f, 90f);
        var hexLabel = Ui.Text(transform, "HEX", UiTheme.TextRole.Label, UiTheme.Tone.Haze);
        Place(hexLabel.rectTransform, columnX, top - 122f, columnWidth, 34f);
        _hex = MakeInput(transform, columnX, top - 178f, Mathf.Min(columnWidth, 190f), 50f);
        _hex.onValueChanged.AddListener(text =>
        {
            if (_hex.isFocused && text.TrimStart('#').Length == 6 && LocalLook.TryParseHex(text, out var color)) SetColor(color);
        });
        _hex.onEndEdit.AddListener(text =>
        {
            if (LocalLook.TryParseHex(text, out var color)) SetColor(color);
            else Show(false);   // not a colour: back to the current one
        });
        var ok = Ui.Button(transform, "OK", UiButton.Variant.Primary, "tick", Close);
        Place((RectTransform)ok.transform, columnX, top - squareSize + 34f, Mathf.Min(columnWidth, 190f), 76f);

        // Presets, two rows of ten.
        var presets = CosmeticsCatalog.Instance != null ? CosmeticsCatalog.Instance.Colors : Array.Empty<CosmeticsCatalog.Swatch>();
        int perRow = Mathf.Max(1, Mathf.CeilToInt(presets.Length / 2f));
        float presetsTop = top - squareSize - 24f - cell / 2f;
        for (int i = 0; i < presets.Length; i++)
        {
            float x = (i % perRow - (perRow - 1) * 0.5f) * cell, y = presetsTop - (i / perRow) * cell;
            var outline = Ui.Circle(transform, "PresetRing " + presets[i].Name, cell * 0.88f, theme.Ink);
            Place(outline.rectTransform, x, y, cell * 0.88f, cell * 0.88f);
            var swatch = Ui.Circle(outline.transform, "Preset", cell * 0.72f, presets[i].Color);
            Place(swatch.rectTransform, 0f, 0f, cell * 0.72f, cell * 0.72f);
            swatch.raycastTarget = true;
            var color = presets[i].Color;
            var button = swatch.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SetColor(color));
            swatch.gameObject.AddComponent<UiFocus>().Target = outline.rectTransform;   // gamepad focus
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
        var theme = UiTheme.Instance;
        var outer = Ui.Circle(parent, "Marker", size, theme.Ink);
        Place(outer.rectTransform, 0f, 0f, size, size);
        var inner = Ui.Circle(outer.transform, "Inner", size - 10f, theme.Paper);
        Place(inner.rectTransform, 0f, 0f, size - 10f, size - 10f);
        return outer.rectTransform;
    }

    /// <summary>An Ink frame behind the square and the hue bar (the kit's outline).</summary>
    private void Frame(float x, float y, float w, float h)
    {
        float o = UiTheme.Instance.Outline;
        var frame = Ui.Box(transform, "Frame", UiTheme.Tone.Ink, 8f);
        Place((RectTransform)frame.transform, x, y, w + 2f * o, h + 2f * o);
    }

    private static TMP_InputField MakeInput(Transform parent, float x, float y, float w, float h)
    {
        var theme = UiTheme.Instance;
        var box = Ui.Box(parent, "Hex", UiTheme.Tone.Paper, 14f);
        var go = box.gameObject;
        go.SetActive(false);   // TMP_InputField wants its parts before it is enabled
        Place((RectTransform)go.transform, x, y, w, h);

        var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(go.transform, false);
        Stretch(viewport, 10f);
        var text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(viewport, false);
        Stretch(text.rectTransform);
        text.font = theme.UiBlack; text.fontSize = theme.Body; text.color = theme.Ink; text.alignment = TextAlignmentOptions.Center;

        var input = go.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.fontAsset = theme.UiBlack;
        input.pointSize = theme.Body;
        input.characterLimit = 7;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.targetGraphic = box.FillImage;
        input.selectionColor = new Color(theme.Brand.r, theme.Brand.g, theme.Brand.b, 0.35f);
        input.caretColor = theme.Ink;
        input.caretWidth = 3;
        go.AddComponent<UiFocus>().Target = (RectTransform)go.transform;
        go.SetActive(true);
        return input;
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

    #endregion
}
