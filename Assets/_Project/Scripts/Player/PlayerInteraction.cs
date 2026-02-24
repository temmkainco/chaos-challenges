using Fusion;
using System.Linq;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    [Networked] public NetworkObject HeldObject { get; private set; }
    public IInteractable CurrentTarget { get; private set; }

    [SerializeField] private float _distance = 3f;
    [SerializeField] private LayerMask _mask;
    [SerializeField] private Transform _eyesPoint;

    private NetworkButtons _previousButtons;
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
        if (!Object.HasInputAuthority) return;
        UpdateLocalTarget();
    }

    private void UpdateLocalTarget()
    {
        if (HeldObject != null)
        {
            CurrentTarget = null;
            return;
        }

        Ray ray = new Ray(_eyesPoint.position, _player.Camera.transform.forward);
        if (Physics.Raycast(ray, out var hit, _distance, _mask))
        {
            CurrentTarget = hit.collider.gameObject.GetComponent<IInteractable>();
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
        if (!GetInput(out NetworkInputData input)) return;

        var pressed = input.Buttons.GetPressed(_previousButtons);

        if (Object.HasInputAuthority && Runner.IsForward)
        {
            if (pressed.WasPressed(_previousButtons, InputButtons.Interact))
            {
                if (HeldObject != null) {
                    RPC_RequestCancelInteraction();
                    return;
                }

                TryLocalInteraction();

                RPC_RequestInteraction(_player.Camera.transform.forward);
            }
        }

        _previousButtons = input.Buttons;
    }

    private void TryLocalInteraction()
    {
        if (CurrentTarget == null || !CurrentTarget.CanBeInteractedWith || CurrentTarget is not ILocalInteractable localInteractable)
            return;

        localInteractable.LocalInteract();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestInteraction(Vector3 lookDir)
    {
        Ray ray = new Ray(_eyesPoint.position, lookDir);

        if (Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, _distance, _mask))
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable.CanBeInteractedWith)
            {
                interactable.Interact(Object.InputAuthority, Object);

                if (!hit.collider.TryGetComponent<ICancellableInteractable>(out var cancellable))
                    return;

                if (cancellable is NetworkBehaviour networkBehaviour)
                {
                    HeldObject = networkBehaviour.Object;
                }
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestCancelInteraction()
    {
        if (HeldObject == null)
            return;

        var networkBehaviour = HeldObject.GetComponent<NetworkBehaviour>();
        if (networkBehaviour is ICancellableInteractable cancellable)
        {
            cancellable.CancelInteraction(Object.InputAuthority, Object);
            HeldObject = null;
        }
    }

}