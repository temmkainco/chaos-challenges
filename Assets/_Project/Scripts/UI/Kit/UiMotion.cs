using DG.Tweening;
using UnityEngine;

/// <summary>
/// The kit's motion, timed by the theme: pops, panel ins and outs, stamps and slides. With reduce motion on there is
/// no overshoot and nothing moves, things only fade. All tweens run on unscaled time (menus work while paused).
/// </summary>
public static class UiMotion
{
    private static UiTheme T => UiTheme.Instance;
    private static bool Reduced => UiTheme.ReduceMotion;

    private static Ease PopEase => Reduced ? Ease.OutCubic : Ease.OutBack;

    /// <summary>Panel or modal in: scale from 0.9 with overshoot and fade.</summary>
    public static Sequence PanelIn(RectTransform target, CanvasGroup group)
    {
        target.DOKill(); group.DOKill();
        var s = DOTween.Sequence().SetUpdate(true);
        group.alpha = 0f;
        s.Join(group.DOFade(1f, T.PanelIn * 0.7f));
        if (!Reduced)
        {
            target.localScale = Vector3.one * 0.9f;
            s.Join(target.DOScale(1f, T.PanelIn).SetEase(Ease.OutBack, T.Overshoot));
        }
        return s;
    }

    public static Sequence PanelOut(RectTransform target, CanvasGroup group)
    {
        target.DOKill(); group.DOKill();
        var s = DOTween.Sequence().SetUpdate(true);
        s.Join(group.DOFade(0f, T.PanelOut).SetEase(Ease.InCubic));
        if (!Reduced) s.Join(target.DOScale(0.94f, T.PanelOut).SetEase(Ease.InCubic));
        return s;
    }

    /// <summary>Title cards and result stamps: slam in from 1.4x.</summary>
    public static Sequence Stamp(RectTransform target, CanvasGroup group)
    {
        target.DOKill(); group.DOKill();
        var s = DOTween.Sequence().SetUpdate(true);
        group.alpha = 0f;
        s.Join(group.DOFade(1f, T.Stamp * 0.4f));
        if (!Reduced)
        {
            target.localScale = Vector3.one * 1.4f;
            s.Join(target.DOScale(1f, T.Stamp).SetEase(Ease.OutBack, T.Overshoot * 1.3f));
        }
        return s;
    }

    /// <summary>Score changes: a quick punch of the number.</summary>
    public static void Punch(RectTransform target, float amount = 0.25f)
    {
        target.DOKill(true);
        target.localScale = Vector3.one;
        if (Reduced) return;
        target.DOPunchScale(Vector3.one * amount, T.PanelIn, 4, 0.6f).SetUpdate(true);
    }

    /// <summary>Slides in from <paramref name="offset"/> to the resting position.</summary>
    public static Tween SlideIn(RectTransform target, Vector2 rest, Vector2 offset, CanvasGroup group = null)
    {
        target.DOKill();
        if (group != null) { group.alpha = 0f; group.DOFade(1f, T.PanelIn * 0.6f).SetUpdate(true); }
        if (Reduced) { target.anchoredPosition = rest; return DOTween.Sequence().SetUpdate(true); }
        target.anchoredPosition = rest + offset;
        return target.DOAnchorPos(rest, T.PanelIn * 1.25f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public static Tween SlideOut(RectTransform target, Vector2 to, CanvasGroup group = null)
    {
        target.DOKill();
        if (group != null) group.DOFade(0f, T.PanelOut).SetUpdate(true);
        if (Reduced) return DOTween.Sequence().SetUpdate(true).AppendInterval(T.PanelOut);
        return target.DOAnchorPos(to, T.PanelOut * 1.25f).SetEase(Ease.InCubic).SetUpdate(true);
    }
}
