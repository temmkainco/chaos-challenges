using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Confetti for big moments: palette-coloured paper bits burst up and flutter down. Off with reduce motion.</summary>
public class Confetti : MonoBehaviour
{
    public static void Burst(Transform parent, Vector2 position, int count = 80)
    {
        if (UiTheme.ReduceMotion) return;
        var theme = UiTheme.Instance;
        var tones = new[] { theme.Accent, theme.Brand, theme.Paper, theme.Good };
        for (int i = 0; i < count; i++)
        {
            var bit = new GameObject("Confetti", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bit.transform.SetParent(parent, false);
            var image = bit.GetComponent<Image>();
            image.color = tones[i % tones.Length];
            image.raycastTarget = false;
            var rt = (RectTransform)bit.transform;
            rt.sizeDelta = UnityEngine.Random.value < 0.3f ? new Vector2(16f, 16f) : new Vector2(26f, 12f);
            if (rt.sizeDelta.x == 16f) image.sprite = UiShapes.Circle;
            rt.anchoredPosition = position;
            rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            var up = position + new Vector2(UnityEngine.Random.Range(-520f, 520f), UnityEngine.Random.Range(180f, 560f));
            var down = up + new Vector2(UnityEngine.Random.Range(-160f, 160f), -UnityEngine.Random.Range(700f, 1100f));
            float t = UnityEngine.Random.Range(1.6f, 2.6f);
            var s = DOTween.Sequence().SetUpdate(true);
            s.Append(rt.DOAnchorPos(up, 0.45f).SetEase(Ease.OutCubic));
            s.Append(rt.DOAnchorPos(down, t).SetEase(Ease.InSine));
            s.Join(rt.DOLocalRotate(new Vector3(0f, 0f, UnityEngine.Random.Range(-720f, 720f)), t + 0.45f, RotateMode.FastBeyond360));
            s.Insert(0.45f + t * 0.7f, image.DOFade(0f, t * 0.3f));
            s.OnComplete(() => Destroy(bit));
        }
    }
}
