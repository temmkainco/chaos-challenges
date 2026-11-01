using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A broadcast lower third: a slanted Brand key ("GOLDEN RAIN!") and a Paper value ("Gold boxes for 4 s"). Slides in
/// from the left, holds, slides out. Anchor it bottom-left in the safe area.
/// </summary>
public class LowerThird : MonoBehaviour
{
    private RectTransform _rt;
    private CanvasGroup _group;
    private TextMeshProUGUI _key, _value;
    private Vector2 _rest;
    private Tween _hide;

    private RectTransform _keyBox, _valueBox;
    private bool _pinned;
    private UiTheme.TextRole _keyRole = UiTheme.TextRole.DisplayM;
    private float _keyHeight = 96f, _valueHeight = 80f, _pad = 44f;

    /// <summary><paramref name="compact"/>: a smaller tag (the round tag in a corner), still at readable text sizes.</summary>
    public static LowerThird Create(Transform parent, bool compact = false)
    {
        var rt = Ui.Rect(parent, "LowerThird");
        Ui.Place(rt, new Vector2(0f, 0f), new Vector2(0f, 140f), new Vector2(1000f, 96f));
        var lt = rt.gameObject.AddComponent<LowerThird>();
        lt._rt = rt;
        lt._rest = rt.anchoredPosition;
        lt._group = rt.gameObject.AddComponent<CanvasGroup>();
        (lt._keyBox, lt._key) = Part(rt, UiTheme.Tone.Brand, compact ? UiTheme.TextRole.Title : UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper);
        (lt._valueBox, lt._value) = Part(rt, UiTheme.Tone.Paper, UiTheme.TextRole.Body, UiTheme.Tone.Ink);
        lt._keyRole = compact ? UiTheme.TextRole.Title : UiTheme.TextRole.DisplayM;
        if (compact) { lt._keyHeight = 64f; lt._valueHeight = 54f; lt._pad = 28f; }
        lt._valueBox.SetAsFirstSibling();   // the key overlaps the value's slanted edge
        rt.gameObject.SetActive(false);
        return lt;
    }

    private static (RectTransform, TextMeshProUGUI) Part(RectTransform parent, UiTheme.Tone fill, UiTheme.TextRole role, UiTheme.Tone text)
    {
        var box = Ui.Box(parent, fill.ToString(), fill, 6f);
        foreach (var image in box.GetComponentsInChildren<Image>()) image.gameObject.AddComponent<UiSkew>();
        var label = Ui.Text(box.transform, "", role, text, TextAlignmentOptions.Center);
        label.GetComponent<ThemedText>().ShrinkToFit = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        Ui.Stretch(label.rectTransform);
        var rt = (RectTransform)box.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
        return (rt, label);
    }

    /// <summary>Moves it (e.g. a round tag top left); it slides in and out from there.</summary>
    public void Dock(Vector2 anchor, Vector2 position, float scale = 1f)
    {
        _rt.anchorMin = _rt.anchorMax = new Vector2(anchor.x, anchor.y);
        _rt.pivot = new Vector2(0f, anchor.y);
        _rt.anchoredPosition = _rest = position;
        _rt.localScale = Vector3.one * scale;
    }

    /// <summary>Stays up until <see cref="Unpin"/>, updating in place (a chaos event and its seconds left).</summary>
    public void Pin(string key, string value, UiTheme.Tone keyTone = UiTheme.Tone.Brand)
    {
        _keyBox.GetComponent<UiBox>().SetFill(keyTone);
        var theme = UiTheme.Instance;
        _key.GetComponent<ThemedText>().Set(_keyRole, theme.TextOn(keyTone) == theme.Ink ? UiTheme.Tone.Ink : UiTheme.Tone.Paper);
        if (_pinned && gameObject.activeSelf)
        {
            if (_key.text != key || _value.text != value) Layout(key, value);
            return;
        }
        _pinned = true;
        Show(key, value, float.MaxValue);
    }

    public void Unpin()
    {
        if (!_pinned) return;
        _pinned = false;
        Hide();
    }

    public void Show(string key, string value, float hold = -1f)
    {
        Layout(key, value);
        gameObject.SetActive(true);
        _hide?.Kill();
        UiMotion.SlideIn(_rt, _rest, new Vector2(-1100f, 0f), _group);
        if (hold < float.MaxValue) _hide = DOVirtual.DelayedCall(hold > 0f ? hold : UiTheme.Instance.LowerThirdHold, Hide).SetUpdate(true);
    }

    private void Layout(string key, string value)
    {
        _key.text = key;
        _value.text = value;
        bool hasValue = !string.IsNullOrEmpty(value);
        _valueBox.gameObject.SetActive(hasValue);
        // Each box fits its text (translations included); the value tucks under the key's slanted edge.
        float pad = _pad, keyWidth = _key.GetPreferredValues(key).x + 2f * pad;
        _keyBox.anchoredPosition = Vector2.zero;
        _keyBox.sizeDelta = new Vector2(keyWidth, _keyHeight);
        if (hasValue)
        {
            _valueBox.anchoredPosition = new Vector2(keyWidth - 24f, -4f);
            _valueBox.sizeDelta = new Vector2(_value.GetPreferredValues(value).x + 2f * pad + 24f, _valueHeight);
            _value.rectTransform.offsetMin = new Vector2(24f, 0f);
        }
    }

    public void Hide()
    {
        _hide?.Kill();
        if (!gameObject.activeSelf) return;
        UiMotion.SlideOut(_rt, _rest + new Vector2(-1100f, 0f), _group).OnComplete(() => gameObject.SetActive(false));
    }
}
