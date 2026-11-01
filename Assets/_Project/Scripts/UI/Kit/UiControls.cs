using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builders for the kit's slider and toggle. Both are Unity's own <see cref="Slider"/> / <see cref="Toggle"/> (so
/// navigation, dragging and events work as usual) dressed in kit boxes, with a <see cref="UiFocus"/> ring.
/// </summary>
public static class UiControls
{
    /// <summary>
    /// A labelled slider row: label on the left, a chunky track with a Paper fill and a round handle, the value on the
    /// right (<paramref name="format"/>, e.g. "{0:0}%").
    /// </summary>
    public static Slider Slider(Transform parent, string label, float min, float max, float value, string format = "{0:0}")
    {
        var row = Ui.Rect(parent, "Slider " + label);
        Ui.Size(row, 860f, 72f);
        var text = Ui.Text(row, label, UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        Ui.Place(text.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(340f, 60f));

        var area = Ui.Rect(row, "Track Area");
        Ui.Place(area, new Vector2(0f, 0.5f), new Vector2(360f, 0f), new Vector2(380f, 72f));
        var track = Ui.Box(area, "Track", UiTheme.Tone.Ink, 14f, false);
        Ui.Place((RectTransform)track.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 28f));
        var fillArea = Ui.Rect(track.transform, "Fill Area");
        Ui.Stretch(fillArea, 5f);
        var fill = Ui.Box(fillArea, "Fill", UiTheme.Tone.Paper, 9f, false);
        fill.HasOutline = false;
        var fillRt = (RectTransform)fill.transform;
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(0f, 1f); fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;

        var handleArea = Ui.Rect(area, "Handle Area");
        Ui.Stretch(handleArea);
        handleArea.offsetMin = new Vector2(18f, 0f); handleArea.offsetMax = new Vector2(-18f, 0f);
        var handle = Ui.Box(handleArea, "Handle", UiTheme.Tone.Accent, 22f);
        var handleRt = (RectTransform)handle.transform;
        handleRt.sizeDelta = new Vector2(44f, 0f);

        var valueText = Ui.Text(row, "", UiTheme.TextRole.Body, UiTheme.Tone.Haze, TextAlignmentOptions.Right);
        Ui.Place(valueText.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, 60f));

        var slider = area.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handle.FillImage;
        slider.minValue = min; slider.maxValue = max;
        slider.onValueChanged.AddListener(v => valueText.text = string.Format(format, v));
        slider.value = value;
        valueText.text = string.Format(format, value);
        area.gameObject.AddComponent<UiFocus>().Target = area;
        return slider;
    }

    /// <summary>A labelled on/off switch; the knob shows a tick or a cross (not colour alone).</summary>
    public static Toggle Toggle(Transform parent, string label, bool on)
    {
        var row = Ui.Rect(parent, "Toggle " + label);
        Ui.Size(row, 860f, 72f);
        var text = Ui.Text(row, label, UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        Ui.Place(text.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(600f, 60f));

        var track = Ui.Box(row, "Switch", UiTheme.Tone.Riser, 30f);
        var trackRt = Ui.Place((RectTransform)track.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 4f), new Vector2(124f, 60f));
        var knob = Ui.Box(trackRt, "Knob", UiTheme.Tone.Paper, 22f, false);
        var knobRt = Ui.Place((RectTransform)knob.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
        var icon = Ui.Icon(knobRt, "cross", 28f, UiTheme.Tone.Ink);

        var toggle = trackRt.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = track.FillImage;
        void Show(bool v)
        {
            track.SetFill(v ? UiTheme.Tone.Good : UiTheme.Tone.Riser);
            knobRt.DOKillAnchor();
            knobRt.anchoredPosition = new Vector2(v ? 28f : -28f, 0f);
            icon.sprite = UiShapes.Icon(v ? "tick" : "cross");
        }
        toggle.onValueChanged.AddListener(v => { Show(v); Sfx.Play(Sound.Click, 0.7f); });
        toggle.isOn = on;
        Show(on);
        trackRt.gameObject.AddComponent<UiFocus>().Target = trackRt;
        return toggle;
    }

    private static void DOKillAnchor(this RectTransform rt) => DG.Tweening.DOTween.Kill(rt);
}
