using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Short notifications at the top centre ("Copied!", "Player 3 joined"): an icon and one line, stacked, each gone
/// after a few seconds. Tone carries meaning together with the icon (tick, cross, !), never by colour alone.
/// </summary>
public class UiToast : MonoBehaviour
{
    private const int MAX = 3;
    private static UiToast _host;
    private readonly List<RectTransform> _shown = new();
    private RectTransform _root;

    public static void Show(string text, UiTheme.Tone tone = UiTheme.Tone.Riser, string icon = null, float seconds = 2.6f)
    {
        if (_host == null)
        {
            var safe = Ui.Canvas("Toasts", 950);
            DontDestroyOnLoad(safe.root.gameObject);
            _host = safe.root.gameObject.AddComponent<UiToast>();
            _host._root = safe;
        }
        _host.Add(text, tone, icon ?? (tone == UiTheme.Tone.Good ? "tick" : tone == UiTheme.Tone.Bad ? "cross" : null), seconds);
    }

    private void Add(string text, UiTheme.Tone tone, string icon, float seconds)
    {
        var box = Ui.Box(_root, "Toast", tone, -1f);
        var rt = (RectTransform)box.transform;
        Ui.Place(rt, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(640f, 80f));
        var row = Ui.Rect(rt, "Row");
        Ui.Stretch(row);
        var layout = Ui.Stack<UnityEngine.UI.HorizontalLayoutGroup>(row, 2, TextAnchor.MiddleCenter);
        layout.padding = new RectOffset(24, 24, 0, 0);
        var theme = UiTheme.Instance;
        var textTone = theme.TextOn(tone) == theme.Ink ? UiTheme.Tone.Ink : UiTheme.Tone.Paper;
        if (icon != null) Ui.Size(Ui.Icon(row, icon, 40f, textTone), 40f, 40f);
        var label = Ui.Text(row, text, UiTheme.TextRole.Body, textTone);
        label.fontStyle = FontStyles.Bold;
        var group = box.gameObject.AddComponent<CanvasGroup>();

        _shown.Insert(0, rt);
        while (_shown.Count > MAX) { var old = _shown[^1]; _shown.RemoveAt(_shown.Count - 1); if (old != null) Destroy(old.gameObject); }
        Restack();
        UiMotion.SlideIn(rt, Slot(0), new Vector2(0f, 60f), group);
        DOVirtual.DelayedCall(seconds, () =>
        {
            if (rt == null) return;
            UiMotion.SlideOut(rt, rt.anchoredPosition + new Vector2(0f, 40f), group).OnComplete(() =>
            {
                _shown.Remove(rt);
                if (rt != null) Destroy(rt.gameObject);
            });
        }).SetUpdate(true);
    }

    private void Restack()
    {
        for (int i = 1; i < _shown.Count; i++)
            if (_shown[i] != null) _shown[i].DOAnchorPos(Slot(i), UiTheme.Instance.PanelIn).SetUpdate(true);
    }

    /// <summary>Below the timer bar (top centre), newest on top.</summary>
    private static Vector2 Slot(int index) => new(0f, -70f - index * 96f);
}
