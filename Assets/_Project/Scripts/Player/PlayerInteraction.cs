using Fusion;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class PlayerInteraction : NetworkBehaviour
{
    [Networked] public NetworkObject HeldObject { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    [SerializeField] private float _distance = 3f;
    [SerializeField] private LayerMask _mask;
    [SerializeField] public Transform ObjectHolder;
    [SerializeField] private Transform EyesPoint;
    public Vector3 PredictedGrabPosition { get; private set; }
    public Quaternion PredictedGrabRotation { get; private set; }

    private IGrabbable _currentTarget;

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority)
            return;

        PredictedGrabPosition = ObjectHolder.position;
        PredictedGrabRotation = ObjectHolder.rotation;

        UpdateTarget();
        HandleInput();
    }

    private void UpdateTarget()
    {
        _currentTarget = null;

        var lookDirection = GetComponent<Player>().Camera.transform.forward;

        Ray ray = new Ray(EyesPoint.position, lookDirection);

        if (Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, _distance, _mask))
        {
            _currentTarget = hit.collider.GetComponent<IGrabbable>();
        }
    }

    private void HandleInput()
    {
        if (!GetInput(out NetworkInputData input)) return;

        var pressed = input.Buttons.GetPressed(PreviousButtons);

        if (pressed.WasPressed(PreviousButtons, InputButtons.Interact))
        {
            if (_currentTarget is GrabbableObject grabbable)
            {
                if (grabbable.CanBeGrabbed)
                {
                    grabbable.RPC_RequestGrab(Object.InputAuthority);
                    HeldObject = grabbable.Object;
                }
                else if (HeldObject == grabbable.Object)
                {
                    grabbable.RPC_RequestRelease(Object.InputAuthority);
                    HeldObject = null;
                }
            }
        }

        PreviousButtons = input.Buttons;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (EyesPoint == null) return;

        Gizmos.color = Color.yellow;

        var player = GetComponent<Player>();
        Vector3 direction = (player != null && player.Camera != null)
            ? player.Camera.transform.forward
            : EyesPoint.forward;

        Gizmos.DrawLine(EyesPoint.position, EyesPoint.position + direction * _distance);
    }
#endif

}
