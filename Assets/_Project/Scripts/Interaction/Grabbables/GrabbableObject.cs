using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabbableObject : NetworkBehaviour, IGrabbable
{
    [Networked, OnChangedRender(nameof(OnGrabbedChanged))]
    public NetworkBool IsGrabbed { get; private set; }

    [Networked] public PlayerRef CurrentHolder { get; private set; }
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

    public override void Spawned()
    {
        _rb.position = transform.position;
        _rb.rotation = transform.rotation;

        if (_myCollider != null)
        {
            _myCollider.enabled = false;
            _myCollider.enabled = true;
        }

        Physics.SyncTransforms();

        if (IsGrabbed)
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;

            if (HolderObject != null)
            {
                ToggleCollisions(HolderObject, true);
                _lastHolder = HolderObject;
            }
        }
    }

    void OnGrabbedChanged()
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

    public override void FixedUpdateNetwork()
    {
        if (IsGrabbed)
        {
            _rb.position = transform.position;
            _rb.rotation = transform.rotation;
        }
    }

    public bool CanBeGrabbed => !IsGrabbed;

    public void Grab(PlayerRef player, NetworkObject playerObject)
    {
        ToggleCollisions(playerObject, true);

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

        _lastHolder = HolderObject;

        CurrentHolder = PlayerRef.None;
        HolderObject = null;
        IsGrabbed = false;

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.WakeUp();

        if (force.sqrMagnitude > 0)
            _rb.AddForce(force, ForceMode.Impulse);
    }

    private void ToggleCollisions(NetworkObject playerObj, bool ignore)
    {
        if (playerObj == null || _myCollider == null) return;

        var colliders = playerObj.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col != _myCollider)
            {
                Physics.IgnoreCollision(_myCollider, col, ignore);
            }
        }
    }
}