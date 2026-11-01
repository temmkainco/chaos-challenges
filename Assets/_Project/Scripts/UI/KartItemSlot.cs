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
    private RectTransform _root;
    private UiBox _frame;
    private Image _icon;
    private TextMeshProUGUI _name, _hint;
    private byte _shown = 255;
    private bool _shownPad;
    private Sprite[] _sprites;

    public static void Create(KartKupMinigameController kart)
    {
        var go = new GameObject("KartItemSlot");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, kart.gameObject.scene);
        go.AddComponent<KartItemSlot>()._kart = kart;
    }

    private void Awake()
    {
        _sprites = new[]
        {
            null,
            Resources.Load<Sprite>("KartItems/Banana"),
            Resources.Load<Sprite>("KartItems/Boost"),
            Resources.Load<Sprite>("KartItems/Shock"),
        };
        var safe = Ui.Canvas("Kart Item", 30, transform);
        _root = Ui.Place(Ui.Rect(safe, "Slot"), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(360f, 250f));
        _frame = Ui.Box(_root, "Frame", UiTheme.Tone.Studio, 28f);
        Ui.Place((RectTransform)_frame.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(160f, 160f));
        _icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        _icon.transform.SetParent(_frame.transform, false);
        _icon.raycastTarget = false;
        _icon.preserveAspect = true;
        Ui.Stretch(_icon.rectTransform, 20f);
        _name = Ui.Text(_root, "", UiTheme.TextRole.Title, UiTheme.Tone.Paper, TextAlignmentOptions.Center, true);
        Ui.Place(_name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 52f));
        _hint = Ui.Text(_root, "", UiTheme.TextRole.Label, UiTheme.Tone.Paper, TextAlignmentOptions.Center, true);
        Ui.Place(_hint.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(360f, 40f));
    }

    private void Update()
    {
        bool live = _kart != null && _kart.Object != null && _kart.Object.IsValid && _kart.IsGameActive && !PauseMenu.IsOpen;
        if (_root.gameObject.activeSelf != live) _root.gameObject.SetActive(live);
        if (!live) return;

        byte item = _kart.LocalItem;
        if (item != _shown || _shownPad != PromptBar.UsingGamepad)
        {
            bool changed = item != _shown;
            _shown = item;
            _shownPad = PromptBar.UsingGamepad;
            bool has = item > 0 && item < _sprites.Length && _sprites[item] != null;
            _icon.sprite = has ? _sprites[item] : null;
            _icon.enabled = has;
            _name.text = item switch { 1 => "BANANA", 2 => "BOOST", 3 => "SHOCKWAVE", _ => "?" };
            _hint.text = item == 0 ? "Drive through an item box" : $"{PromptBar.Cap("E")} to use";
            _frame.SetFill(item == 0 ? UiTheme.Tone.Studio : UiTheme.Tone.Accent);
            if (has && changed) { Sfx.Play(Sound.Pop, 0.8f, 0.1f); UiMotion.Punch((RectTransform)_frame.transform, 0.2f); }
        }
        // Ready to use: the hint pulses (not with reduce motion).
        float pulse = item == 0 || UiTheme.ReduceMotion ? 0f : 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
        _hint.transform.localScale = Vector3.one * (1f + 0.1f * pulse);
    }
}
