using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// The show's logo lockup: a pill (ON AIR, PLEASE STAND BY) over DON'T STOP / THE PAR[TV], the TV a tilted Brand box
/// that rocks gently. The rect is as wide as the lockup, so it can sit in a corner (main menu) or be centred (the
/// stand-by curtain).
/// </summary>
public class ShowLogo : MonoBehaviour
{
    public const float HEIGHT = 340f;

    public RectTransform Rect => (RectTransform)transform;

    public static ShowLogo Create(Transform parent, string pill, UiTheme.Tone pillTone = UiTheme.Tone.Bad)
    {
        var theme = UiTheme.Instance;
        var rt = Ui.Rect(parent, "Logo");
        rt.sizeDelta = new Vector2(1000f, HEIGHT);
        var logo = rt.gameObject.AddComponent<ShowLogo>();

        var box = Ui.Box(rt, "Pill", pillTone, 28f);
        var label = Ui.Text(box.transform, pill, UiTheme.TextRole.Label, theme.TextOn(pillTone) == theme.Ink ? UiTheme.Tone.Ink : UiTheme.Tone.Paper);
        var themed = label.GetComponent<ThemedText>();
        themed.Spacing = 14f;
        themed.SingleLine = true;
        themed.Apply();
        float labelWidth = label.GetPreferredValues(pill).x;
        Ui.Place((RectTransform)box.transform, new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(labelWidth + 76f, 56f));
        var dot = Ui.Circle(box.transform, "Dot", 18f, label.color);
        Ui.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(18f, 18f));
        if (!UiTheme.ReduceMotion)
            dot.DOFade(0.25f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(dot.gameObject);
        Ui.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(48f, 0f);
        label.rectTransform.offsetMax = new Vector2(-20f, 0f);

        var line1 = Ui.Text(rt, "DON'T STOP", UiTheme.TextRole.DisplayXL, UiTheme.Tone.Paper, TextAlignmentOptions.Left, true);
        Ui.Place(line1.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 136f));
        var line2 = Ui.Text(rt, $"THE <color=#{ColorUtility.ToHtmlStringRGB(theme.Brand)}>PAR</color>", UiTheme.TextRole.DisplayXL,
            UiTheme.Tone.Paper, TextAlignmentOptions.Left, true);
        Ui.Place(line2.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -196f), new Vector2(1000f, 136f));

        line1.ForceMeshUpdate();
        line2.ForceMeshUpdate();
        float width = line2.textBounds.max.x;   // the pivot is the line's left edge
        var tvBox = Ui.Box(rt, "TV", UiTheme.Tone.Brand, 26f);
        var tv = Ui.Place((RectTransform)tvBox.transform, new Vector2(0f, 1f), new Vector2(width + 18f, -196f), new Vector2(196f, 132f));
        var tvText = Ui.Text(tv, "TV", UiTheme.TextRole.DisplayL, UiTheme.Tone.Paper, TextAlignmentOptions.Center, true);
        Ui.Stretch(tvText.rectTransform);
        tv.pivot = new Vector2(0.5f, 0.5f);
        tv.anchoredPosition += new Vector2(98f, -66f);
        tv.localRotation = Quaternion.Euler(0f, 0f, -4f);
        if (!UiTheme.ReduceMotion)
            tv.DOLocalRotate(new Vector3(0f, 0f, 2f), 1.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(tv.gameObject);

        // As wide as the lockup itself.
        rt.sizeDelta = new Vector2(Mathf.Max(line1.textBounds.max.x, width + 18f + 196f) + 8f, HEIGHT);
        return logo;
    }
}
