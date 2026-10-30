using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Short-lived world-space text ("BONK!", "HOT!", "ESCAPED!") that pops up, floats and fades. Local only; the text
/// objects are pooled, so frequent popups do not allocate.
/// </summary>
public static class WorldPopup
{
    private static readonly Stack<TextMeshPro> Pool = new();

    public static void Show(Vector3 position, string text, Color color, float size = 7f)
    {
        TextMeshPro tmp = null;
        while (Pool.Count > 0 && tmp == null)
            tmp = Pool.Pop();   // entries die with their scene
        if (tmp == null)
            tmp = new GameObject("Popup").AddComponent<TextMeshPro>();
        var go = tmp.gameObject;
        go.SetActive(true);
        go.transform.position = position;
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
            .OnComplete(() =>
            {
                go.SetActive(false);
                Pool.Push(tmp);
            })
            .SetLink(go);
    }
}
