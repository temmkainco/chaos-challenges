using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A player's identity everywhere in the UI: a colour disc with their number (colour is never the only cue), their
/// name and an optional status icon (ready tick, out cross, crown).
/// </summary>
public class PlayerChip : MonoBehaviour
{
    private Image _disc;
    private TextMeshProUGUI _number, _name;
    private Image _status;

    public static PlayerChip Create(Transform parent, float height = 72f)
    {
        var box = Ui.Box(parent, "PlayerChip", UiTheme.Tone.Riser, height / 2f, true);
        box.ShadowDepth = UiTheme.Instance.ShadowChip;
        box.Apply();
        var rt = (RectTransform)box.transform;
        rt.sizeDelta = new Vector2(380f, height);
        var chip = rt.gameObject.AddComponent<PlayerChip>();
        float disc = height - 16f;
        var ring = Ui.Circle(rt, "DiscRing", disc + 8f, UiTheme.Instance.Ink);
        Ui.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(disc + 8f, disc + 8f));
        chip._disc = Ui.Circle(ring.transform, "Disc", disc, Color.white);
        chip._number = Ui.Text(chip._disc.transform, "", UiTheme.TextRole.Title, UiTheme.Tone.Ink);
        Ui.Stretch(chip._number.rectTransform);
        chip._name = Ui.Text(rt, "", UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        chip._name.fontStyle = FontStyles.Bold;
        var nameRt = chip._name.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 0f); nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(disc + 24f, 0f); nameRt.offsetMax = new Vector2(-60f, 0f);
        chip._status = Ui.Icon(rt, "tick", 40f, UiTheme.Tone.Good);
        Ui.Place(chip._status.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(40f, 40f));
        chip._status.gameObject.SetActive(false);
        return chip;
    }

    /// <summary><paramref name="status"/>: null, "tick" (ready), "cross" (out) or "star" (leader).</summary>
    public void Set(int number, Color color, string name, string status = null)
    {
        _disc.color = color;
        _number.text = number.ToString();
        _name.text = name;
        // Names stay on one line; very long ones shrink a little, then end in "…".
        var themed = _name.GetComponent<ThemedText>();
        if (!themed.SingleLine) { themed.SingleLine = true; themed.Apply(); }
        _name.overflowMode = TextOverflowModes.Ellipsis;
        _status.gameObject.SetActive(status != null);
        if (status == null) return;
        _status.sprite = UiShapes.Icon(status);
        _status.color = UiTheme.Instance.Color(status == "cross" ? UiTheme.Tone.Bad : status == "star" ? UiTheme.Tone.Accent : UiTheme.Tone.Good);
    }
}
