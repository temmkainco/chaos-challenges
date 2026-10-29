using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>Short-lived world-space text ("BANG!", "Nope!", "ESCAPED!") that pops up, floats and fades. Local only.</summary>
public static class WorldPopup
{
    public static void Show(Vector3 position, string text, Color color, float size = 7f)
    {
        var go = new GameObject("Popup");
        go.transform.position = position;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;

        var camera = Camera.main;
        if (camera != null)
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - camera.transform.position);

        go.transform.localScale = Vector3.zero;
        DOTween.Sequence()
            .Append(go.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
            .Join(go.transform.DOMoveY(position.y + 1.2f, 1.2f))
            .AppendInterval(0.4f)
            .Append(go.transform.DOScale(0f, 0.2f))
            .OnComplete(() => Object.Destroy(go))
            .SetLink(go);
    }
}
