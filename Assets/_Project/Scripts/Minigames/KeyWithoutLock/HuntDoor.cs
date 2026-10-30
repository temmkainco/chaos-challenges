using DG.Tweening;
using UnityEngine;

/// <summary>
/// One numbered door in Key Hunt. Local visuals only (swing open for an escape, rattle for a wrong key); the
/// minigame controller decides which keys fit. The door faces into the room along its forward axis.
/// </summary>
public class HuntDoor : MonoBehaviour
{
    [SerializeField] private int _id;
    [SerializeField, Tooltip("Rotates when the door opens")] private Transform _hinge;

    public int Id => _id;
    /// <summary>Where a contestant stands to try a key.</summary>
    public Vector3 FrontPosition => transform.position + transform.forward * 1.4f;

    public void Configure(int id, Transform hinge)
    {
        _id = id;
        _hinge = hinge;
    }

    public void PlayOpen()
    {
        if (_hinge == null) return;
        _hinge.DOKill(true);
        DOTween.Sequence()
            .Append(_hinge.DOLocalRotate(new Vector3(0f, -100f, 0f), 0.35f).SetEase(Ease.OutBack))
            .AppendInterval(1.2f)
            .Append(_hinge.DOLocalRotate(Vector3.zero, 0.4f).SetEase(Ease.InQuad))
            .SetLink(gameObject);
    }

    public void PlayReject()
    {
        if (_hinge == null) return;
        _hinge.DOKill(true);
        _hinge.DOShakeRotation(0.4f, new Vector3(0f, 8f, 0f), 18).SetLink(gameObject);
    }
}
