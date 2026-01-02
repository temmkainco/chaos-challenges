using Fusion;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class PlayerInteraction : NetworkBehaviour
{
    [Networked] public NetworkObject HeldObject { get; private set; }

    [SerializeField] private float _distance = 3f;
    [SerializeField] private LayerMask _mask;
    [SerializeField] public Transform ObjectHolder;
    [SerializeField] private Transform EyesPoint;

    private IGrabbable _currentTarget;
    private GrabbableObject _currentTargetObj;
    private NetworkButtons _previousButtons;

    public override void FixedUpdateNetwork()
    {
        // IMPORTANT: Only process input on InputAuthority, but RPCs work from any client
        if (!Object.HasInputAuthority) return;

        UpdateTarget();

        if (!GetInput(out NetworkInputData input)) return;

        var pressed = input.Buttons.GetPressed(_previousButtons);

        if (pressed.WasPressed(_previousButtons, InputButtons.Interact))
        {
            Debug.Log($"Interact pressed! HeldObject: {HeldObject}, CurrentTarget: {_currentTarget}");

            if (HeldObject == null)
            {
                // Try to grab
                TryGrab();
            }
            else
            {
                // Release held object
                TryRelease();
            }
        }

        _previousButtons = input.Buttons;
    }

    private void UpdateTarget()
    {
        // Clear previous target highlight
        if (_currentTargetObj != null)
        {
            _currentTargetObj.OnUntargeted();
        }

        _currentTarget = null;
        _currentTargetObj = null;

        // Don't look for new targets if holding something
        if (HeldObject != null) return;

        var player = GetComponent<Player>();
        if (player?.Camera == null || EyesPoint == null) return;

        var ray = new Ray(EyesPoint.position, player.Camera.transform.forward);

        if (Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, _distance, _mask))
        {
            _currentTarget = hit.collider.GetComponent<IGrabbable>();
            _currentTargetObj = hit.collider.GetComponent<GrabbableObject>();

            // Highlight target - use Render to ensure visual feedback shows immediately
            if (_currentTargetObj != null && _currentTarget != null && _currentTarget.CanBeGrabbed)
            {
                _currentTargetObj.OnTargeted();
            }
        }
    }

    // Also clear target on disable
    private void OnDisable()
    {
        if (_currentTargetObj != null)
        {
            _currentTargetObj.OnUntargeted();
            _currentTargetObj = null;
        }
    }

    private void TryGrab()
    {
        if (_currentTarget == null || !_currentTarget.CanBeGrabbed)
        {
            Debug.Log($"Cannot grab - Target null: {_currentTarget == null}, Can grab: {_currentTarget?.CanBeGrabbed}");
            return;
        }

        // Send RPC to state authority to grab
        var targetObj = (_currentTarget as GrabbableObject);
        if (targetObj != null && targetObj.Object != null)
        {
            Debug.Log($"Requesting grab of {targetObj.name}. HasInputAuthority: {Object.HasInputAuthority}, HasStateAuthority: {Object.HasStateAuthority}");

            // If we ARE the state authority, just call directly
            if (Object.HasStateAuthority)
            {
                Debug.Log("We are state authority, calling grab directly");
                PerformGrab(targetObj.Object, Object.InputAuthority);
            }
            else
            {
                Debug.Log("Sending RPC to state authority");
                RPC_RequestGrab(targetObj.Object);
            }
        }
        else
        {
            Debug.Log($"Failed to get grabbable object or NetworkObject");
        }
    }

    // Extracted logic so it can be called directly or via RPC
    private void PerformGrab(NetworkObject targetObject, PlayerRef player)
    {
        Debug.Log($"[PerformGrab] Called for {targetObject.name} by {player}");

        if (targetObject == null)
        {
            Debug.LogWarning("[PerformGrab] Target object is null");
            return;
        }

        var grabbable = targetObject.GetComponent<IGrabbable>();
        if (grabbable == null)
        {
            Debug.LogWarning($"[PerformGrab] No IGrabbable found on {targetObject.name}");
            return;
        }

        if (!grabbable.CanBeGrabbed)
        {
            Debug.LogWarning($"[PerformGrab] Object cannot be grabbed (already held by {grabbable.CurrentHolder})");
            return;
        }

        // Perform grab
        Debug.Log($"[PerformGrab] Grabbing {targetObject.name} for player {player}");
        grabbable.Grab(player);
        HeldObject = targetObject;
    }

    private void TryRelease()
    {
        if (HeldObject == null) return;

        Debug.Log($"Requesting release. HasStateAuthority: {Object.HasStateAuthority}");

        // If we ARE the state authority, just call directly
        if (Object.HasStateAuthority)
        {
            var grabbable = HeldObject.GetComponent<IGrabbable>();
            if (grabbable != null)
            {
                grabbable.Release(Object.InputAuthority);
                HeldObject = null;
            }
        }
        else
        {
            RPC_RequestRelease(HeldObject);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestGrab(NetworkObject targetObject, RpcInfo info = default)
    {
        Debug.Log($"[Server RPC] Grab request received from {info.Source}");
        PerformGrab(targetObject, info.Source);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRelease(NetworkObject targetObject, RpcInfo info = default)
    {
        Debug.Log($"[Server RPC] Release request received from {info.Source}");

        if (targetObject == null) return;

        var grabbable = targetObject.GetComponent<IGrabbable>();
        if (grabbable == null) return;
        if (grabbable.CurrentHolder != info.Source) return;

        // Perform release
        grabbable.Release(info.Source);
        HeldObject = null;
    }

    // Cleanup if object is despawned while held
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Only access networked properties if the object is still valid and spawned
        if (HeldObject != null && HeldObject.IsValid)
        {
            var grabbable = HeldObject.GetComponent<IGrabbable>();
            if (grabbable != null)
            {
                // Check if we can safely access networked properties
                if (Object != null && Object.IsValid)
                {
                    grabbable.Release(Object.InputAuthority);
                }
            }
        }
        HeldObject = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (EyesPoint == null) return;

        var player = GetComponent<Player>();
        if (player?.Camera == null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(EyesPoint.position, EyesPoint.position + transform.forward * _distance);
        }
        else
        {
            Gizmos.color = HeldObject != null ? Color.green : (_currentTarget != null ? Color.cyan : Color.yellow);
            Gizmos.DrawLine(EyesPoint.position, EyesPoint.position + player.Camera.transform.forward * _distance);
        }
    }
#endif
}