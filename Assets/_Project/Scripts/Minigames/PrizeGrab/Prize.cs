using System.Collections.Generic;
using DG.Tweening;
using Fusion;
using UnityEngine;

/// <summary>
/// A Prize Grab prize. Carried and thrown with <see cref="GrabbableObject"/> (its carry speed slows the holder).
/// <see cref="Value"/> 1 shows the gift box, 3 the golden chest.
/// </summary>
[RequireComponent(typeof(GrabbableObject))]
public class Prize : NetworkBehaviour
{
    /// <summary>Live instances on this peer (no scene searches in the simulation).</summary>
    public static readonly List<Prize> Active = new();

    [Networked, OnChangedRender(nameof(ApplyLook))] public byte Value { get; set; }

    [SerializeField] private GameObject _gift;
    [SerializeField] private GameObject _golden;
    [SerializeField] private Transform _visual;

    public GrabbableObject Grabbable { get; private set; }

    private void Awake()
    {
        Grabbable = GetComponent<GrabbableObject>();
    }

    public override void Spawned()
    {
        if (!Active.Contains(this)) Active.Add(this);
        ApplyLook();
        if (_visual != null)
        {
            _visual.localScale = Vector3.zero;
            _visual.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
        }
    }

    private void ApplyLook()
    {
        bool golden = Value > 1;
        if (_gift != null) _gift.SetActive(!golden);
        if (_golden != null) _golden.SetActive(golden);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
    }
}
