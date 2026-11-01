using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The show timer. Quiet most of the time (a thin bar emptying at the top centre), loud in the last
/// <see cref="UiTheme.TimerLoudSeconds"/>: a big outlined count in the middle that pulses and ticks each second and
/// turns Warning. <see cref="Set"/> it every frame from the game's clock.
/// </summary>
public class ShowTimer : MonoBehaviour
{
    private RectTransform _fill, _count;
    private TextMeshProUGUI _countText;
    private UiBox _fillBox;
    private int _shownSecond = -1;

    public static ShowTimer Create(Transform parent)
    {
        var rt = Ui.Rect(parent, "ShowTimer");
        Ui.Stretch(rt);
        var timer = rt.gameObject.AddComponent<ShowTimer>();
        var track = Ui.Box(rt, "Track", UiTheme.Tone.Studio, 14f);
        var trackRt = Ui.Place((RectTransform)track.transform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(520f, 30f));
        var fillArea = Ui.Rect(trackRt, "FillArea");
        Ui.Stretch(fillArea, 5f);
        timer._fillBox = Ui.Box(fillArea, "Fill", UiTheme.Tone.Paper, 9f, false);
        timer._fillBox.HasOutline = false;
        timer._fill = (RectTransform)timer._fillBox.transform;
        timer._fill.anchorMin = Vector2.zero; timer._fill.anchorMax = Vector2.one; timer._fill.offsetMin = timer._fill.offsetMax = Vector2.zero;
        timer._countText = Ui.Text(rt, "", UiTheme.TextRole.DisplayXL, UiTheme.Tone.Paper, TextAlignmentOptions.Center, true);
        timer._count = timer._countText.rectTransform;
        Ui.Place(timer._count, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(400f, 220f));
        timer._count.gameObject.SetActive(false);
        return timer;
    }

    public void Set(float remaining, float total)
    {
        var theme = UiTheme.Instance;
        float t = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        _fill.anchorMax = new Vector2(t, 1f);
        bool loud = remaining > 0f && remaining <= theme.TimerLoudSeconds;
        _fillBox.SetFill(loud ? UiTheme.Tone.Warn : UiTheme.Tone.Paper);
        _count.gameObject.SetActive(loud);
        if (!loud) { _shownSecond = -1; return; }
        int second = Mathf.CeilToInt(remaining);
        if (second == _shownSecond) return;
        _shownSecond = second;
        _countText.text = second.ToString();
        _countText.GetComponent<ThemedText>().Set(UiTheme.TextRole.DisplayXL, second <= 3 ? UiTheme.Tone.Bad : UiTheme.Tone.Warn, true);
        UiMotion.Punch(_count, 0.3f);
        Sfx.Play(Sound.Tick, second <= 3 ? 1f : 0.6f);
    }
}
