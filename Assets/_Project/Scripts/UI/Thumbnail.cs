using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame pictures in the game list, the rules card and the intermission screen: clipped to a rounded rectangle (the
/// kit's panel shape) and scaled to cover their frame without stretching, so no picture pokes over its frame's edge.
/// </summary>
public static class Thumbnail
{
    /// <summary>Makes <paramref name="clip"/> a rounded mask (instead of a square RectMask2D).</summary>
    public static void Round(GameObject clip)
    {
        if (clip.TryGetComponent<RectMask2D>(out var square)) Object.DestroyImmediate(square);
        if (!clip.TryGetComponent<Image>(out var shape)) shape = clip.AddComponent<Image>();
        shape.sprite = UiKit.Instance != null ? UiKit.Instance.Panel : null;
        shape.type = Image.Type.Sliced;
        shape.color = Color.white;
        shape.raycastTarget = false;
        if (!clip.TryGetComponent<Mask>(out var mask)) mask = clip.AddComponent<Mask>();
        mask.showMaskGraphic = false;
    }

    /// <summary>Sizes <paramref name="picture"/> to cover <paramref name="frame"/> (centred, cropped, never stretched).</summary>
    public static void Cover(Image picture, Vector2 frame)
    {
        var sprite = picture.sprite;
        picture.preserveAspect = false;
        if (sprite == null || frame.y <= 0f) return;
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        var size = aspect > frame.x / frame.y ? new Vector2(frame.y * aspect, frame.y) : new Vector2(frame.x, frame.x / aspect);
        var rt = picture.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    /// <summary>
    /// For a picture laid out in a scene: puts it inside a rounded mask the size of its own rect, so it is cropped to
    /// rounded corners and covers that rect. Safe to call more than once.
    /// </summary>
    public static void Frame(Image picture)
    {
        if (picture == null) return;
        var rt = picture.rectTransform;
        var parent = rt.parent as RectTransform;
        if (parent == null || parent.name == "RoundedFrame")
        {
            if (parent != null) Cover(picture, parent.rect.size);
            return;
        }
        var frame = new GameObject("RoundedFrame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(parent, false);
        frame.SetSiblingIndex(rt.GetSiblingIndex());
        frame.anchorMin = rt.anchorMin;
        frame.anchorMax = rt.anchorMax;
        frame.pivot = rt.pivot;
        frame.anchoredPosition = rt.anchoredPosition;
        frame.sizeDelta = rt.sizeDelta;
        frame.localScale = rt.localScale;
        frame.localRotation = rt.localRotation;
        var size = rt.rect.size;
        rt.SetParent(frame, false);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        Round(frame.gameObject);
        Cover(picture, size);
    }
}
