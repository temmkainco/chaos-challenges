using DG.Tweening;
using UnityEngine;

/// <summary>Idle life for the menu logo: the TV wobbles and the contestant behind it peeks out now and then.</summary>
public class MenuLogo : MonoBehaviour
{
    [SerializeField] private RectTransform _tv;
    [SerializeField] private RectTransform _peeker;

    private void OnEnable()
    {
        if (_tv != null)
        {
            _tv.DOKill();
            _tv.localRotation = Quaternion.Euler(0f, 0f, -6f);
            _tv.DOLocalRotate(new Vector3(0f, 0f, 3f), 1.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
            _tv.DOPunchScale(Vector3.one * 0.12f, 0.6f, 6).SetDelay(0.3f).SetLink(gameObject);
        }
        if (_peeker != null)
        {
            var hidden = _peeker.anchoredPosition;
            var shown = hidden + new Vector2(-46f, 18f);
            DOTween.Sequence()
                .AppendInterval(1.2f)
                .Append(_peeker.DOAnchorPos(shown, 0.35f).SetEase(Ease.OutBack))
                .AppendInterval(1.6f)
                .Append(_peeker.DOAnchorPos(hidden, 0.3f).SetEase(Ease.InBack))
                .AppendInterval(2.4f)
                .SetLoops(-1)
                .SetLink(gameObject);
        }
    }
}
