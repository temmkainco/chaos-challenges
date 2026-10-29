using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// Host-authoritative grab, carry and throw. The host owns the rigidbody: while held it is kinematic and
/// follows the holder; on release it is thrown along the holder's look direction. Every peer snaps the
/// held object to its holder in LateUpdate (after Fusion's interpolation) so it never trails behind a predicted player.
/// </summary>
[RequireComponent(typeof(NetworkRigidbody3D))]
public class GrabbableObject : NetworkBehaviour, IGrabbable, ICancellableInteractable
{
    [Networked, OnChangedRender(nameof(OnGrabbedChanged))]
    public NetworkBool IsGrabbed { get; private set; }

    public bool CanBeInteractedWith => !IsGrabbed;
    public Outline Outline { get; private set; }

    [Networked] public PlayerRef CurrentHolder { get; private set; }
    [Networked] public NetworkObject HolderObject { get; private set; }

    [Header("Hold Settings")]
    public Vector3 _holdPositionOffset = new Vector3(0, 2, 0);
    [SerializeField] private float _throwForce = 5f;
    [SerializeField, Tooltip("Extra lift so throws arc instead of hitting the ground")] private float _throwLift = 1.5f;

    private NetworkRigidbody3D _nrb;
    private Collider _collider;

    private void Awake()
    {
        _nrb = GetComponent<NetworkRigidbody3D>();
        _collider = GetComponent<Collider>();
        Outline = GetComponent<Outline>();
        if (Outline != null)
            Outline.enabled = false;
    }

    public override void Spawned()
    {
        ApplyHeldState();
    }

    private void OnGrabbedChanged()
    {
        Sfx.Play(Sound.Pop, 0.7f, 0.1f);
        ApplyHeldState();
    }

    /// <summary>Colliders are local state, so every peer applies them; the host also owns the rigidbody mode.</summary>
    private void ApplyHeldState()
    {
        if (_collider != null)
            _collider.enabled = !IsGrabbed;

        if (HasStateAuthority)
        {
            _nrb.Rigidbody.isKinematic = IsGrabbed;
            _nrb.Rigidbody.useGravity = !IsGrabbed;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !IsGrabbed)
            return;

        // The holder left or was despawned.
        if (HolderObject == null)
        {
            Release(Vector3.zero);
            return;
        }

        FollowHolder();
    }

    private void LateUpdate()
    {
        if (Object != null && Object.IsValid && IsGrabbed && HolderObject != null)
            transform.SetPositionAndRotation(HoldPosition, HolderObject.transform.rotation);
    }

    public void Interact(PlayerRef player, NetworkObject playerObject)
    {
        if (!HasStateAuthority || IsGrabbed) return;

        CurrentHolder = player;
        HolderObject = playerObject;
        IsGrabbed = true;

        _nrb.Rigidbody.linearVelocity = Vector3.zero;
        _nrb.Rigidbody.angularVelocity = Vector3.zero;
        ApplyHeldState();
        FollowHolder();
    }

    public void CancelInteraction(PlayerRef player, NetworkObject playerObject)
    {
        if (!HasStateAuthority || !IsGrabbed || player != CurrentHolder) return;

        Vector3 look = playerObject.TryGetComponent<PlayerInteraction>(out var interaction) && interaction.LookDirection.sqrMagnitude > 0.01f
            ? interaction.LookDirection
            : playerObject.transform.forward;

        Release(look * _throwForce + Vector3.up * _throwLift);
    }

    private Vector3 HoldPosition => HolderObject.transform.TransformPoint(_holdPositionOffset);

    private void FollowHolder()
    {
        Vector3 position = HoldPosition;
        Quaternion rotation = HolderObject.transform.rotation;
        transform.SetPositionAndRotation(position, rotation);
        _nrb.Rigidbody.position = position;
        _nrb.Rigidbody.rotation = rotation;
    }

    private void Release(Vector3 impulse)
    {
        CurrentHolder = PlayerRef.None;
        HolderObject = null;
        IsGrabbed = false;
        ApplyHeldState();

        _nrb.Rigidbody.WakeUp();
        if (impulse.sqrMagnitude > 0f)
            _nrb.Rigidbody.AddForce(impulse, ForceMode.Impulse);
    }
}
