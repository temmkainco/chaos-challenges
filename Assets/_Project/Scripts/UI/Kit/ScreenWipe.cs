using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The stinger between screens: Brand and Accent bands sweep across (slanted), cover the screen while
/// <paramref name="covered"/> runs (load the next screen there), then sweep off. A fade with reduce motion.
/// </summary>
public static class ScreenWipe
{
    public static void Play(Action covered)
    {
        var theme = UiTheme.Instance;
        var safe = Ui.Canvas("Wipe", 990);
        var canvas = (RectTransform)safe.parent;
        UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
        var group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        var bands = new[] { (UiTheme.Tone.Accent, 0f), (UiTheme.Tone.Brand, 0.08f), (UiTheme.Tone.Ink, 0.16f) };
        var seq = DOTween.Sequence().SetUpdate(true);
        float width = 3600f, half = theme.Wipe / 2f;
        foreach (var (tone, delay) in bands)
        {
            var band = new GameObject("Band", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UiSkew));
            band.transform.SetParent(canvas, false);
            band.GetComponent<Image>().color = theme.Color(tone);
            band.GetComponent<UiSkew>().Angle = 14f;
            var rt = (RectTransform)band.transform;
            rt.anchorMin = new Vector2(0.5f, -0.1f); rt.anchorMax = new Vector2(0.5f, 1.1f); rt.sizeDelta = new Vector2(width, 0f);
            if (UiTheme.ReduceMotion) { rt.anchoredPosition = Vector2.zero; continue; }
            rt.anchoredPosition = new Vector2(-width, 0f);
            seq.Insert(delay, rt.DOAnchorPosX(0f, half).SetEase(Ease.OutCubic));
            seq.Insert(half + 0.15f + delay, rt.DOAnchorPosX(width, half).SetEase(Ease.InCubic));
        }
        if (UiTheme.ReduceMotion)
        {
            group.alpha = 0f;
            seq.Append(group.DOFade(1f, half)).AppendCallback(() => covered?.Invoke()).Append(group.DOFade(0f, half));
        }
        else seq.InsertCallback(half + 0.16f, () => covered?.Invoke());
        seq.OnComplete(() => UnityEngine.Object.Destroy(canvas.gameObject));
    }
}
