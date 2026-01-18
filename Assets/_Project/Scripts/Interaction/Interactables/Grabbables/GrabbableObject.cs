using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

[RequireComponent(typeof(NetworkRigidbody3D))]
[RequireComponent(typeof(AuthorityHandler))]
public class GrabbableObject : NetworkBehaviour, IGrabbable, ICancellableInteractable
{
    [Networked, OnChangedRender(nameof(OnGrabbedChanged))]
    public NetworkBool IsGrabbed { get; private set; }

    public bool CanBeInteractedWith => !IsGrabbed;


    [Networked] public PlayerRef CurrentHolder { get; private set; }
    [Networked] public NetworkObject HolderObject { get; private set; }

    [Header("Hold Settings")]
    public Vector3 _holdPositionOffset = new Vector3(0, 2, 0);
    [SerializeField] private float _throwForce = 5f;

    private NetworkRigidbody3D _nrb;
    private Collider _collider;
    private NetworkObject _lastHolder;

    private void Awake()
    {
        _nrb = GetComponent<NetworkRigidbody3D>();
        _collider = GetComponent<Collider>();
    }

    public override void Spawned()
    {
        if (_collider != null)
        {
            _collider.enabled = false;
            _collider.enabled = true;
        }

        Physics.SyncTransforms();

        if (IsGrabbed && HolderObject != null)
        {
            _nrb.Rigidbody.isKinematic = true;
            _nrb.Rigidbody.useGravity = false;
            _collider.enabled = false;
            _lastHolder = HolderObject;

            transform.SetParent(HolderObject.transform);
            transform.localPosition = _holdPositionOffset;
            transform.localRotation = Quaternion.identity;
        }
    }

    void OnGrabbedChanged()
    {
        if (IsGrabbed && HolderObject != null)
        {
            _nrb.Rigidbody.isKinematic = true;
            _nrb.Rigidbody.useGravity = false;
            _collider.enabled = false;
            _lastHolder = HolderObject;

            transform.SetParent(HolderObject.transform);
            transform.localPosition = _holdPositionOffset;
            transform.localRotation = Quaternion.identity;
        }
        else if (!IsGrabbed && _lastHolder != null)
        {
            _nrb.Rigidbody.isKinematic = false;
            _nrb.Rigidbody.useGravity = true;
            _collider.enabled = true;
            _lastHolder = null;

            transform.SetParent(null);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsGrabbed || HolderObject == null)
            return;

        _nrb.Rigidbody.position = transform.position;
        _nrb.Rigidbody.rotation = transform.rotation;
    }

    public override void Render()
    {
        if (!IsGrabbed || HolderObject == null)
            return;

        _nrb.Rigidbody.position = transform.position;
        _nrb.Rigidbody.rotation = transform.rotation;
    }


    public void Interact(PlayerRef player, NetworkObject playerObject)
    {
        if (!Object.HasStateAuthority) return;

        CurrentHolder = player;
        HolderObject = playerObject;
        IsGrabbed = true;

        _nrb.Rigidbody.isKinematic = true;
        _nrb.Rigidbody.useGravity = false;

        _collider.enabled = false;

        transform.SetParent(playerObject.transform);
        transform.localPosition = _holdPositionOffset;
        transform.localRotation = Quaternion.identity;

        _nrb.Rigidbody.position = transform.position;
        _nrb.Rigidbody.rotation = transform.rotation;
    }

    public void CancelInteraction(PlayerRef player, NetworkObject playerObject)
    {
        if (!Object.HasStateAuthority) return;

        _lastHolder = HolderObject;

        CurrentHolder = PlayerRef.None;
        HolderObject = null;
        IsGrabbed = false;

        _nrb.Rigidbody.isKinematic = false;
        _nrb.Rigidbody.useGravity = true;
        _nrb.Rigidbody.WakeUp();

        _collider.enabled = true;

        transform.SetParent(null);
        
        var playerBehaviour = playerObject.GetComponent<Player>();

        var force = playerBehaviour != null
            ? playerBehaviour.Camera.transform.forward * _throwForce
            : Vector3.zero;

        if (force.sqrMagnitude > 0)
        {
            _nrb.Rigidbody.AddForce(force, ForceMode.Impulse);
        }
    }
}