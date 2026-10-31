using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kart Kup's item slot, bottom centre: empty ("?" and a hint to hit an item box) or the item you're holding, with its
/// picture (Resources/KartItems) and a pulsing "PRESS E" so it's obvious it's ready to use. Local UI, built by the
/// minigame on every peer.
/// </summary>
public class KartItemSlot : MonoBehaviour
{
    private KartKupMinigameController _kart;
    private Image _frame, _icon;
    private TMP_Text _name, _hint;
    private byte _shown = 255;
    private Sprite[] _sprites;

    public static void Create(KartKupMinigameController kart)
    {
        var go = new GameObject("KartItemSlot", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, kart.gameObject.scene);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<KartItemSlot>()._kart = kart;
    }

    private void Awake()
    {
        var kit = UiKit.Instance;
        _sprites = new[]
        {
            null,
            Resources.Load<Sprite>("KartItems/Banana"),
            Resources.Load<Sprite>("KartItems/Boost"),
            Resources.Load<Sprite>("KartItems/Shock"),
        };
        var root = new GameObject("Slot", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 120f);
        root.sizeDelta = new Vector2(170f, 230f);
        _frame = Image(root, kit.Panel, UiKit.Cream, 0f, 30f, 150f, 150f);
        _icon = Image(root, null, Color.white, 0f, 30f, 120f, 120f);
        _name = Text(root, 30f, 0f, -70f, 300f, 40f);
        _hint = Text(root, 26f, 0f, -104f, 300f, 36f);
    }

    private void Update()
    {
        bool live = _kart != null && _kart.Object != null && _kart.Object.IsValid && _kart.IsGameActive;
        if (_frame.gameObject.activeSelf != live)
            foreach (Transform child in _frame.transform.parent) child.gameObject.SetActive(live);
        if (!live) return;

        byte item = _kart.LocalItem;
        if (item != _shown)
        {
            _shown = item;
            bool has = item > 0 && item < _sprites.Length && _sprites[item] != null;
            _icon.sprite = has ? _sprites[item] : null;
            _icon.color = has ? Color.white : Color.clear;
            _name.text = item switch { 1 => "BANANA", 2 => "BOOST", 3 => "SHOCKWAVE", _ => "?" };
            _hint.text = item == 0 ? "<size=80%>drive through an item box</size>" : "PRESS <color=#E8590C>E</color>";
            if (has) Sfx.Play(Sound.Pop, 0.8f, 0.1f);
        }
        // Ready to use: the slot glows and the hint pulses.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
        _frame.color = item == 0 ? new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.6f) : Color.Lerp(UiKit.Cream, UiKit.Yellow, pulse);
        _hint.transform.localScale = Vector3.one * (item == 0 ? 1f : 1f + 0.12f * pulse);
        _icon.transform.localScale = Vector3.one * (item == 0 ? 1f : 1f + 0.06f * pulse);
    }

    private static Image Image(Transform parent, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        var go = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return image;
    }

    private static TMP_Text Text(Transform parent, float size, float x, float y, float w, float h)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        var kit = UiKit.Instance;
        text.font = kit.Font;
        if (kit.OutlineThick != null) text.fontSharedMaterial = kit.OutlineThick;
        text.fontSize = size;
        text.color = UiKit.Cream;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return text;
    }
}
