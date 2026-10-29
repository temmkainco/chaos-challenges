using DG.Tweening;
using Fusion;
using UnityEngine;

/// <summary>
/// A key for the vault door. Grabbed and thrown with <see cref="GrabbableObject"/>; whether it fits is known
/// only to the host (<see cref="KeyLockMinigameController"/>). Keys come in a few colours that mean nothing.
/// </summary>
[RequireComponent(typeof(GrabbableObject))]
public class VaultKey : NetworkBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly Color[] Colors =
    {
        new(1f, 0.82f, 0.25f), new(0.8f, 0.83f, 0.88f), new(0.85f, 0.5f, 0.3f), new(0.45f, 0.75f, 1f), new(0.95f, 0.45f, 0.7f),
    };

    [Networked, OnChangedRender(nameof(ApplyColor))] public byte Variant { get; set; }

    [SerializeField] private Renderer[] _renderers;
    [SerializeField] private Transform _visual;

    public GrabbableObject Grabbable { get; private set; }

    private void Awake()
    {
        Grabbable = GetComponent<GrabbableObject>();
    }

    public override void Spawned()
    {
        ApplyColor();
        if (_visual != null)
        {
            // Idle bob so keys lying around catch the eye.
            _visual.DOLocalMoveY(_visual.localPosition.y + 0.15f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(gameObject);
        }
    }

    private void ApplyColor()
    {
        var block = new MaterialPropertyBlock();
        var color = Colors[Variant % Colors.Length];
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            r.SetPropertyBlock(block);
        }
    }
}
