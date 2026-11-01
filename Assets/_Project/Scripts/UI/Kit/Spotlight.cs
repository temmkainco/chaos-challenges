using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The winner reveal's light: the screen dims to the theme's scrim except a soft spotlight pool on
/// <paramref name="screenPoint"/> (canvas units from the centre). Fades in.
/// </summary>
public static class Spotlight
{
    public static GameObject Show(Transform parent, Vector2 screenPoint, float radius = 420f)
    {
        var theme = UiTheme.Instance;
        var root = Ui.Rect(parent, "Spotlight");
        Ui.Stretch(root);
        root.SetAsFirstSibling();
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dim.transform.SetParent(root, false);
        Ui.Stretch((RectTransform)dim.transform);
        dim.GetComponent<Image>().color = new Color(theme.Ink.r, theme.Ink.g, theme.Ink.b, theme.ScrimAlpha);
        dim.GetComponent<Image>().raycastTarget = false;
        var pool = new GameObject("Pool", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pool.transform.SetParent(root, false);
        var image = pool.GetComponent<Image>();
        image.sprite = UiShapes.Glow;
        image.color = new Color(1f, 0.93f, 0.66f, 0.55f);
        image.raycastTarget = false;
        Ui.Place((RectTransform)pool.transform, new Vector2(0.5f, 0.5f), screenPoint, new Vector2(radius * 2f, radius * 2.6f));
        group.alpha = 0f;
        group.DOFade(1f, UiTheme.ReduceMotion ? 0.2f : 0.6f).SetUpdate(true);
        return root.gameObject;
    }
}
