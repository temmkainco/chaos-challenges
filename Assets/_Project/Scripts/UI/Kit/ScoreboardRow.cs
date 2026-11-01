using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A broadcast scoreboard row: rank block, player chip disc + name, points. The leader's rank block is Accent and the
/// row gets the focus ring; points count up when they change, with the gain shown beside them.
/// </summary>
public class ScoreboardRow : MonoBehaviour
{
    private UiBox _rank, _ring;
    private TextMeshProUGUI _rankText, _name, _points, _gain;
    private Image _disc;
    private TextMeshProUGUI _number;
    private int _shownPoints;

    public static ScoreboardRow Create(Transform parent, float width = 640f, float height = 88f)
    {
        var rt = Ui.Rect(parent, "ScoreRow");
        rt.sizeDelta = new Vector2(width, height);
        var row = rt.gameObject.AddComponent<ScoreboardRow>();
        var theme = UiTheme.Instance;
        row._ring = Ui.Box(rt, "LeaderRing", UiTheme.Tone.Accent, -1f, false);
        row._ring.HasOutline = false;
        Ui.Stretch((RectTransform)row._ring.transform, -(theme.FocusRing + theme.Outline) + 2f);
        var box = Ui.Box(rt, "Box", UiTheme.Tone.Studio);
        Ui.Stretch((RectTransform)box.transform);
        row._rank = Ui.Box(box.transform, "Rank", UiTheme.Tone.Riser, -1f, false);
        Ui.Place((RectTransform)row._rank.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(height, height));
        row._rankText = Ui.Text(row._rank.transform, "", UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper);
        Ui.Stretch(row._rankText.rectTransform);
        var ring = Ui.Circle(box.transform, "DiscRing", 54f, theme.Ink);
        Ui.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(height + 18f, 0f), new Vector2(54f, 54f));
        row._disc = Ui.Circle(ring.transform, "Disc", 44f, Color.white);
        row._number = Ui.Text(row._disc.transform, "", UiTheme.TextRole.Label, UiTheme.Tone.Ink);
        Ui.Stretch(row._number.rectTransform);
        row._name = Ui.Text(box.transform, "", UiTheme.TextRole.Title, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        var n = row._name.rectTransform;
        n.anchorMin = Vector2.zero; n.anchorMax = Vector2.one; n.offsetMin = new Vector2(height + 88f, 0f); n.offsetMax = new Vector2(-170f, 0f);
        row._points = Ui.Text(box.transform, "", UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper, TextAlignmentOptions.Right);
        Ui.Place(row._points.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(150f, height));
        row._gain = Ui.Text(rt, "", UiTheme.TextRole.DisplayM, UiTheme.Tone.Good, TextAlignmentOptions.Left, true);
        Ui.Place(row._gain.rectTransform, new Vector2(1f, 0.5f), new Vector2(150f, 0f), new Vector2(140f, height));
        row._gain.gameObject.SetActive(false);
        return row;
    }

    public void Set(int rank, int number, Color color, string name, int points, bool leader)
    {
        _rankText.text = rank.ToString();
        _rank.SetFill(leader ? UiTheme.Tone.Accent : UiTheme.Tone.Riser);
        _rankText.color = UiTheme.Instance.Color(leader ? UiTheme.Tone.Ink : UiTheme.Tone.Paper);
        _ring.gameObject.SetActive(leader);
        _disc.color = color;
        _number.text = number.ToString();
        _name.text = name;
        _shownPoints = points;
        _points.text = points.ToString();
    }

    /// <summary>Counts the points up to <paramref name="points"/> and shows the gain beside the row.</summary>
    public void AddPoints(int points)
    {
        int from = _shownPoints, gain = points - from;
        _shownPoints = points;
        if (gain > 0)
        {
            _gain.text = "+" + gain;
            _gain.gameObject.SetActive(true);
            UiMotion.Punch(_gain.rectTransform);
        }
        DOVirtual.Int(from, points, UiTheme.ReduceMotion ? 0.01f : 0.8f, v => _points.text = v.ToString()).SetUpdate(true)
            .OnComplete(() => UiMotion.Punch(_points.rectTransform, 0.2f));
    }
}
