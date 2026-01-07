using Fusion;
using System.Linq;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    [Networked] public NetworkObject HeldObject { get; private set; }

    [SerializeField] private float _distance = 3f;
    [SerializeField] private LayerMask _mask;
    [SerializeField] private Transform _eyesPoint;
    [SerializeField] private string _handBoneName = "Hand.R";
    [SerializeField] private float _throwForce = 5f;

    [SerializeField] private Transform _handTransform;
    private NetworkButtons _previousButtons;
    private Player _player;

    public GrabbableObject CurrentTarget { get; private set; }

    public override void Spawned()
    {
        _player = GetComponent<Player>();

        if (Object.HasStateAuthority || Object.HasInputAuthority)
        {
            Runner.SetPlayerObject(Object.InputAuthority, Object);
        }


        _handTransform = System.Array.Find(GetComponentsInChildren<Transform>(true),
                t => t.name == _handBoneName);
    }

    public Transform GetHandTransform() => _handTransform;

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
            CurrentTarget = hit.collider.GetComponentInParent<GrabbableObject>();
        }
        else
        {
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
                if (HeldObject == null)
                {
                    RPC_RequestGrab(_player.Camera.transform.forward);
                }
                else
                {
                    RPC_RequestRelease(_player.Camera.transform.forward * _throwForce);
                }
            }
        }

        _previousButtons = input.Buttons;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestGrab(Vector3 lookDir)
    {
        Ray ray = new Ray(_eyesPoint.position, lookDir);

        if (Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, _distance, _mask))
        {
            var grabbable = hit.collider.GetComponentInParent<GrabbableObject>();
            if (grabbable != null && grabbable.CanBeGrabbed)
            {
                grabbable.Grab(Object.InputAuthority, Object);
                HeldObject = grabbable.Object;
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRelease(Vector3 force)
    {
        if (HeldObject == null) return;
        var grabbable = HeldObject.GetComponent<GrabbableObject>();
        if (grabbable != null) grabbable.Release(force);
        HeldObject = null;
    }
}