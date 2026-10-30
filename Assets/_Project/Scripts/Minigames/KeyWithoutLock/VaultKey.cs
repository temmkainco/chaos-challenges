using System.Collections.Generic;
using DG.Tweening;
using Fusion;
using UnityEngine;

/// <summary>
/// A Key Hunt key. Carried with <see cref="GrabbableObject"/> (keys cannot be thrown). Which door it opens
/// (<see cref="DoorId"/>) is set by the host and never shown; after a wrong try the key remembers how close its
/// door was (<see cref="Heat"/>) and glows cold blue, warm orange or hot red.
/// </summary>
[RequireComponent(typeof(GrabbableObject))]
public class VaultKey : NetworkBehaviour
{
    /// <summary>Live instances on this peer (no scene searches in the simulation).</summary>
    public static readonly List<VaultKey> Active = new();

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    public static readonly string[] Names = { "GOLD", "SILVER", "COPPER", "BLUE", "PINK" };
    public static readonly Color[] Colors =
    {
        new(1f, 0.82f, 0.25f), new(0.8f, 0.83f, 0.88f), new(0.85f, 0.5f, 0.3f), new(0.45f, 0.75f, 1f), new(0.95f, 0.45f, 0.7f),
    };

    [Networked, OnChangedRender(nameof(ApplyColor))] public byte Variant { get; set; }
    /// <summary>The door this key opens (host only cares; not shown anywhere).</summary>
    [Networked] public byte DoorId { get; set; }
    /// <summary>0 = untried, 1 = cold, 2 = warm, 3 = hot (its door is close to the last door tried).</summary>
    [Networked, OnChangedRender(nameof(ApplyColor))] public byte Heat { get; set; }

    private static readonly Color[] HeatColors = { Color.white, new(0.35f, 0.65f, 1f), new(1f, 0.6f, 0.2f), new(1f, 0.2f, 0.15f) };

    [SerializeField] private Renderer[] _renderers;
    [SerializeField] private Transform _visual;

    public GrabbableObject Grabbable { get; private set; }

    private void Awake()
    {
        Grabbable = GetComponent<GrabbableObject>();
    }

    public override void Spawned()
    {
        if (!Active.Contains(this)) Active.Add(this);
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
        var color = Heat > 0 ? HeatColors[Mathf.Min(Heat, (byte)3)] : Colors[Variant % Colors.Length];
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            r.SetPropertyBlock(block);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
    }
}
