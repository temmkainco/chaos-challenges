using DG.Tweening;
using UnityEngine;

/// <summary>The exit of Key Without Lock. Local visuals only: the door swings open for an escape and rattles for a dud.</summary>
public class VaultDoor : MonoBehaviour
{
    [SerializeField] private Transform _hinge;
    [SerializeField] private Transform _lock;
    [SerializeField] private Transform _front;
    [SerializeField] private Transform _escapeSpot;

    /// <summary>Where players stand to try a key.</summary>
    public Vector3 FrontPosition => _front.position;
    public Vector3 Forward => _front.forward;
    /// <summary>Where escaped contestants wait (behind the door).</summary>
    public Vector3 EscapePosition => _escapeSpot.position;

    public void PlayOpen()
    {
        _hinge.DOKill(true);
        DOTween.Sequence()
            .Append(_hinge.DOLocalRotate(new Vector3(0f, -100f, 0f), 0.35f).SetEase(Ease.OutBack))
            .AppendInterval(1f)
            .Append(_hinge.DOLocalRotate(Vector3.zero, 0.4f).SetEase(Ease.InQuad))
            .SetLink(gameObject);
        _lock.DOKill(true);
        _lock.DOPunchScale(Vector3.one * 0.4f, 0.4f);
    }

    public void PlayReject()
    {
        _lock.DOKill(true);
        _lock.DOShakeRotation(0.4f, new Vector3(0f, 0f, 35f), 20);
        _hinge.DOKill(true);
        _hinge.DOShakePosition(0.3f, 0.06f, 30);
    }
}
