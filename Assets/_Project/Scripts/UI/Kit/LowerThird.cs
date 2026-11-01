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

    public static LowerThird Create(Transform parent)
    {
        var rt = Ui.Rect(parent, "LowerThird");
        Ui.Place(rt, new Vector2(0f, 0f), new Vector2(0f, 140f), new Vector2(1000f, 96f));
        var lt = rt.gameObject.AddComponent<LowerThird>();
        lt._rt = rt;
        lt._rest = rt.anchoredPosition;
        lt._group = rt.gameObject.AddComponent<CanvasGroup>();
        (lt._keyBox, lt._key) = Part(rt, UiTheme.Tone.Brand, UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper);
        (lt._valueBox, lt._value) = Part(rt, UiTheme.Tone.Paper, UiTheme.TextRole.Body, UiTheme.Tone.Ink);
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

    public void Show(string key, string value, float hold = -1f)
    {
        _key.text = key;
        _value.text = value;
        bool hasValue = !string.IsNullOrEmpty(value);
        _valueBox.gameObject.SetActive(hasValue);
        // Each box fits its text (translations included); the value tucks under the key's slanted edge.
        float pad = 44f, keyWidth = _key.GetPreferredValues(key).x + 2f * pad;
        _keyBox.anchoredPosition = Vector2.zero;
        _keyBox.sizeDelta = new Vector2(keyWidth, 96f);
        if (hasValue)
        {
            _valueBox.anchoredPosition = new Vector2(keyWidth - 24f, -4f);
            _valueBox.sizeDelta = new Vector2(_value.GetPreferredValues(value).x + 2f * pad + 24f, 80f);
            _value.rectTransform.offsetMin = new Vector2(24f, 0f);
        }
        gameObject.SetActive(true);
        _hide?.Kill();
        UiMotion.SlideIn(_rt, _rest, new Vector2(-1100f, 0f), _group);
        _hide = DOVirtual.DelayedCall(hold > 0f ? hold : UiTheme.Instance.LowerThirdHold, Hide).SetUpdate(true);
    }

    public void Hide()
    {
        _hide?.Kill();
        UiMotion.SlideOut(_rt, _rest + new Vector2(-1100f, 0f), _group).OnComplete(() => gameObject.SetActive(false));
    }
}
