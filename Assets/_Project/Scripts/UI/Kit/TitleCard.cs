using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A round's title card / result stamp: a small Brand tag above a big display word ("ROUND 2" / "PRIZE GRAB",
/// "QUALIFIED", "WINNER!"), slammed in with a sting. Outlined so it reads over the 3D world.
/// </summary>
public class TitleCard : MonoBehaviour
{
    private RectTransform _rt;
    private CanvasGroup _group;
    private TextMeshProUGUI _tag, _title;
    private GameObject _tagBox;

    public static TitleCard Create(Transform parent)
    {
        var rt = Ui.Rect(parent, "TitleCard");
        Ui.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1400f, 300f));
        var card = rt.gameObject.AddComponent<TitleCard>();
        card._rt = rt;
        card._group = rt.gameObject.AddComponent<CanvasGroup>();
        var tag = Ui.Box(rt, "Tag", UiTheme.Tone.Brand, 10f);
        foreach (var image in tag.GetComponentsInChildren<Image>()) image.gameObject.AddComponent<UiSkew>();
        Ui.Place((RectTransform)tag.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(420f, 70f));
        card._tagBox = tag.gameObject;
        card._tag = Ui.Text(tag.transform, "", UiTheme.TextRole.Title, UiTheme.Tone.Paper);
        Ui.Stretch(card._tag.rectTransform);
        card._title = Ui.Text(rt, "", UiTheme.TextRole.DisplayXL, UiTheme.Tone.Paper, TextAlignmentOptions.Center, true);
        Ui.Place(card._title.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1400f, 200f));
        rt.gameObject.SetActive(false);
        return card;
    }

    /// <summary>Shows the card. <paramref name="tone"/> colours the big word (Accent for winners, Good / Bad for stamps).</summary>
    public void Show(string tag, string title, UiTheme.Tone tone = UiTheme.Tone.Paper, float hold = 1.6f, Action done = null)
    {
        _tag.text = tag;
        _tagBox.SetActive(!string.IsNullOrEmpty(tag));
        _title.text = title;
        _title.GetComponent<ThemedText>().Set(UiTheme.TextRole.DisplayXL, tone, true);
        gameObject.SetActive(true);
        Sfx.Play(Sound.Go, 0.9f);
        var s = UiMotion.Stamp(_rt, _group);
        if (hold > 0f)
            s.AppendInterval(hold).Append(_group.DOFade(0f, UiTheme.Instance.PanelOut)).OnComplete(() => { gameObject.SetActive(false); done?.Invoke(); });
    }
}
