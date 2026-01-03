using Fusion;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class PlayerInteraction : NetworkBehaviour
{
    [Networked] public NetworkObject HeldObject { get; private set; }

    [SerializeField] private float _distance = 3f;
    [SerializeField] private LayerMask _mask;
    [SerializeField] private Transform _eyesPoint;

    [Header("Hand Configuration")]
    [SerializeField] private string _rightHandBoneName = "Hand.R";
    [SerializeField] private string _leftHandBoneName = "Hand.L";
    [SerializeField] private bool _useRightHand = true;

    private Transform _handTransform;
    private IGrabbable _currentTarget;
    private GrabbableObject _currentTargetObj;
    private NetworkButtons _previousButtons;

    private void Start()
    {
        FindHandTransform();
    }

    private void FindHandTransform()
    {
        string boneName = _useRightHand ? _rightHandBoneName : _leftHandBoneName;

        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (child.name == boneName)
            {
                _handTransform = child;
                return;
            }
        }
    }

    public Transform GetHandTransform() => _handTransform;

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority) return;

        UpdateTarget();

        if (!GetInput(out NetworkInputData input)) return;

        var pressed = input.Buttons.GetPressed(_previousButtons);

        if (pressed.WasPressed(_previousButtons, InputButtons.Interact))
        {
            if (HeldObject == null)
            {
                TryGrab();
            }
            else
            {
                TryRelease();
            }
        }

        _previousButtons = input.Buttons;
    }

    private void UpdateTarget()
    {
        _currentTarget = null;
        _currentTargetObj = null;

        if (HeldObject != null) return;

        var player = GetComponent<Player>();
        if (player?.Camera == null || _eyesPoint == null)
            return;
        

        var ray = new Ray(_eyesPoint.position, player.Camera.transform.forward);

        if (Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, _distance, _mask))
        {
            _currentTarget = hit.collider.GetComponent<IGrabbable>();
            _currentTargetObj = hit.collider.GetComponent<GrabbableObject>();
        }
    }

    private void OnDisable()
    {
        if (_currentTargetObj != null)
        {
            _currentTargetObj = null;
        }
    }

    private void TryGrab()
    {
        if (_currentTarget == null || !_currentTarget.CanBeGrabbed)
            return;
 

        if (_handTransform == null)
            return;
        

        var targetObj = (_currentTarget as GrabbableObject);
        if (targetObj != null && targetObj.Object != null)
        {
            if (Object.HasStateAuthority)
            {
                PerformGrab(targetObj.Object, Object.InputAuthority);
            }
            else
            {
                RPC_RequestGrab(targetObj.Object);
            }
        }
    }

    private void PerformGrab(NetworkObject targetObject, PlayerRef player)
    {
        if (targetObject == null) return;

        var grabbable = targetObject.GetComponent<IGrabbable>();
        if (grabbable == null || !grabbable.CanBeGrabbed) return;

        Transform gripTransform = GetHandTransform();
        if (gripTransform == null)
            return;

        grabbable.Grab(player, gripTransform);
        HeldObject = targetObject;
    }

    private void TryRelease()
    {
        if (HeldObject == null) return;

        if (Object.HasStateAuthority)
        {
            PerformRelease(Object.InputAuthority);
        }
        else
        {
            RPC_RequestRelease(HeldObject);
        }
    }

    private void PerformRelease(PlayerRef player)
    {
        if (HeldObject == null) return;

        var grabbable = HeldObject.GetComponent<IGrabbable>();
        if (grabbable == null) return;

        grabbable.Release(player);
        HeldObject = null;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestGrab(NetworkObject targetObject, RpcInfo info = default)
    {
        PerformGrab(targetObject, info.Source);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRelease(NetworkObject targetObject, RpcInfo info = default)
    {
        if (targetObject == null) return;

        var grabbable = targetObject.GetComponent<IGrabbable>();
        if (grabbable == null || grabbable.CurrentHolder != info.Source) return;

        PerformRelease(info.Source);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HeldObject != null && HeldObject.IsValid)
        {
            var grabbable = HeldObject.GetComponent<IGrabbable>();
            if (grabbable != null && Object != null && Object.IsValid)
            {
                grabbable.Release(Object.InputAuthority);
            }
        }
        HeldObject = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_eyesPoint == null) return;

        var player = GetComponent<Player>();
        Gizmos.color = HeldObject != null ? Color.green : (_currentTarget != null ? Color.cyan : Color.yellow);

        if (player?.Camera == null)
        {
            Gizmos.DrawLine(_eyesPoint.position, _eyesPoint.position + transform.forward * _distance);
        }
        else
        {
            Gizmos.DrawLine(_eyesPoint.position, _eyesPoint.position + player.Camera.transform.forward * _distance);
        }

        if (_handTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_handTransform.position, 0.1f);
            Gizmos.DrawLine(_handTransform.position, _handTransform.position + _handTransform.forward * 0.2f);
        }
    }
#endif
}