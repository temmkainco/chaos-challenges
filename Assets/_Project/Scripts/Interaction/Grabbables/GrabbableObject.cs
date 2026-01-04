using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : NetworkBehaviour, IGrabbable
{
    [Networked] public PlayerRef CurrentHolder { get; private set; }
    [Networked] public NetworkBool IsGrabbed { get; private set; }
    [Networked] public NetworkObject HolderObject { get; private set; }

    [Header("Hold Settings")]
    public Vector3 _holdPositionOffset = Vector3.zero;
    public Vector3 _holdRotationOffset = Vector3.zero;

    private Rigidbody _rb;
    private Collider _myCollider;
    private NetworkObject _lastHolder;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _myCollider = GetComponent<Collider>();
    }

    public bool CanBeGrabbed => !IsGrabbed;

    public void Grab(PlayerRef player, NetworkObject playerObject)
    {
        if (!Object.HasStateAuthority) return;

        CurrentHolder = player;
        HolderObject = playerObject;
        IsGrabbed = true;

        _rb.isKinematic = true;
        _rb.useGravity = false;
    }

    public void Release(Vector3 force)
    {
        if (!Object.HasStateAuthority) return;

        ToggleCollisions(HolderObject, false);

        CurrentHolder = PlayerRef.None;
        HolderObject = null;
        IsGrabbed = false;

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.WakeUp();

        if (force.sqrMagnitude > 0)
            _rb.AddForce(force, ForceMode.Impulse);
    }

    public override void Render()
    {
        if (IsGrabbed && HolderObject != null)
        {
            ToggleCollisions(HolderObject, true);
            _lastHolder = HolderObject;
        }
        else if (!IsGrabbed && _lastHolder != null)
        {
            ToggleCollisions(_lastHolder, false);
            _lastHolder = null;
        }
    }

    private void ToggleCollisions(NetworkObject playerObj, bool ignore)
    {
        if (playerObj == null || _myCollider == null) return;

        var colliders = playerObj.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            Physics.IgnoreCollision(_myCollider, col, ignore);
        }
    }
}