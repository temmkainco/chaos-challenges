using Fusion;
using UnityEngine;

/// <summary>
/// Interaction runs from the networked input: the host raycasts along the player's
/// <see cref="NetworkInputData.LookDirection"/> and applies the result, so every client sees the same outcome.
/// The owning client only adds instant local feedback (<see cref="ILocalInteractable"/>) and the focus highlight.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [Networked] public NetworkObject HeldObject { get; private set; }
    /// <summary>Last aim direction from input; throws use it.</summary>
    [Networked] public Vector3 LookDirection { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    public IInteractable CurrentTarget { get; private set; }

    [SerializeField] private float _distance = 3f;
    [SerializeField] private LayerMask _mask;
    [SerializeField] private Transform _eyesPoint;

    private Player _player;
    private FocusHighlighter _focusHighlighter = new();

    public override void Spawned()
    {
        _player = GetComponent<Player>();

        if (!Object.HasStateAuthority && !Object.HasInputAuthority)
            return;

        Runner.SetPlayerObject(Object.InputAuthority, Object);
    }

    void Update()
    {
        if (Object == null || !Object.HasInputAuthority) return;
        UpdateLocalTarget();
    }

    private void UpdateLocalTarget()
    {
        if (HeldObject != null)
        {
            _focusHighlighter.Clear();
            CurrentTarget = null;
            return;
        }

        Ray ray = new Ray(_eyesPoint.position, _player.Camera.transform.forward);
        if (Physics.Raycast(ray, out var hit, _distance, _mask))
        {
            CurrentTarget = hit.collider.GetComponentInParent<IInteractable>();
            _focusHighlighter.Highlight(CurrentTarget);
        }
        else
        {
            _focusHighlighter.Clear();
            CurrentTarget = null;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
            ClearStaleHeldObject();

        if (!GetInput(out NetworkInputData input)) return;
        if (_player.ControlsLocked)
            input.Buttons = default;

        bool interactPressed = input.Buttons.WasPressed(PreviousButtons, InputButtons.Interact);
        PreviousButtons = input.Buttons;

        if (input.LookDirection.sqrMagnitude > 0.01f)
            LookDirection = input.LookDirection.normalized;

        if (!interactPressed)
            return;

        if (Object.HasInputAuthority && Runner.IsForward && HeldObject == null)
            TryLocalInteraction();

        if (!HasStateAuthority)
            return;

        var minigame = MinigameController.Current;
        if (minigame != null && minigame.HandlePrimaryAction(_player))
            return;

        if (HeldObject != null)
            ReleaseHeldObject();
        else
            InteractWithTarget();
    }

    private void TryLocalInteraction()
    {
        if (CurrentTarget == null || !CurrentTarget.CanBeInteractedWith || CurrentTarget is not ILocalInteractable localInteractable)
            return;

        localInteractable.LocalInteract();
    }

    /// <summary>Host only.</summary>
    private void InteractWithTarget()
    {
        if (!Runner.GetPhysicsScene().Raycast(_eyesPoint.position, LookDirection, out var hit, _distance, _mask))
            return;

        var interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable == null || !interactable.CanBeInteractedWith)
            return;

        interactable.Interact(Object.InputAuthority, Object);

        if (interactable is ICancellableInteractable && interactable is NetworkBehaviour networkBehaviour)
            HeldObject = networkBehaviour.Object;
    }

    /// <summary>Host only.</summary>
    private void ReleaseHeldObject()
    {
        if (HeldObject.TryGetComponent<ICancellableInteractable>(out var cancellable))
            cancellable.CancelInteraction(Object.InputAuthority, Object);

        HeldObject = null;
    }

    /// <summary>Host only. The object may have been despawned or dropped without us.</summary>
    private void ClearStaleHeldObject()
    {
        if (HeldObject == null)
            return;

        if (!HeldObject.TryGetComponent<IGrabbable>(out var grabbable) || grabbable.CurrentHolder != Object.InputAuthority)
            HeldObject = null;
    }
}
