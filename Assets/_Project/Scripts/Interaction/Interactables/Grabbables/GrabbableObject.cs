using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// Host-authoritative grab, carry, drop and aimed throw. The host owns the rigidbody: while held it is kinematic and
/// sits in the holder's right hand; dropping lets it fall, throwing sets its velocity directly (mass independent, so
/// the local arc preview matches). Every peer snaps the held object to the hand in LateUpdate, after animation.
/// </summary>
[RequireComponent(typeof(NetworkRigidbody3D))]
public class GrabbableObject : NetworkBehaviour, IGrabbable, ICancellableInteractable
{
    public const float MIN_THROW_SPEED = 6f;
    public const float MAX_THROW_SPEED = 19f;
    /// <summary>Throws leave a little above the camera's aim so a level aim still arcs.</summary>
    public const float THROW_PITCH_BONUS = 10f;

    [Networked, OnChangedRender(nameof(OnGrabbedChanged))]
    public NetworkBool IsGrabbed { get; private set; }

    public bool CanBeInteractedWith => Object != null && Object.IsValid && !IsGrabbed;
    public Outline Outline { get; private set; }

    [Networked] public PlayerRef CurrentHolder { get; private set; }
    [Networked] public NetworkObject HolderObject { get; private set; }

    [Header("Hold Settings")]
    [SerializeField, Tooltip("Offset from the right hand bone, in the holder's space (x right, y up, z forward)")] private Vector3 _handOffset = new Vector3(0.05f, -0.05f, 0.18f);
    [SerializeField] private Vector3 _handEuler;
    [SerializeField, Tooltip("Off for items that may only be carried and dropped (Key Hunt keys)")] private bool _canThrow = true;

    [SerializeField, Range(0.3f, 1f), Tooltip("Holder's speed multiplier while carrying this (heavy prizes slow you down)")] private float _carrySpeed = 1f;

    public bool CanThrow => _canThrow;
    public float CarrySpeed => _carrySpeed;

    private NetworkRigidbody3D _nrb;
    private Collider _collider;

    private void Awake()
    {
        _nrb = GetComponent<NetworkRigidbody3D>();
        _collider = GetComponent<Collider>();
        Outline = GetComponent<Outline>();
        if (Outline == null)
        {
            Outline = gameObject.AddComponent<Outline>();
            Outline.OutlineMode = Outline.Mode.OutlineVisible;
            Outline.OutlineColor = new Color(1f, 0.85f, 0.25f);
            Outline.OutlineWidth = 6f;
        }
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
        if (IsGrabbed && Outline != null)
            Outline.enabled = false;

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
        {
            GetHoldPose(out var position, out var rotation);
            transform.SetPositionAndRotation(position, rotation);
        }
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

    /// <summary>Drop at the holder's feet.</summary>
    public void CancelInteraction(PlayerRef player, NetworkObject playerObject)
    {
        if (!HasStateAuthority || !IsGrabbed || player != CurrentHolder) return;

        // Put it just in front so it does not land inside the holder's capsule.
        var front = playerObject.transform.position + playerObject.transform.forward * 0.7f + Vector3.up * 0.9f;
        transform.position = front;
        _nrb.Rigidbody.position = front;
        Release(playerObject.transform.forward * 1.5f);
    }

    /// <summary>Host only: throw along <paramref name="aim"/> with <paramref name="charge"/> in [0, 1].</summary>
    public void Throw(PlayerRef player, Vector3 aim, float charge)
    {
        if (!HasStateAuthority || !IsGrabbed || player != CurrentHolder) return;
        Release(ThrowVelocity(aim, charge));
    }

    /// <summary>Shared by the host and the local arc preview.</summary>
    public static Vector3 ThrowVelocity(Vector3 aim, float charge)
    {
        if (aim.sqrMagnitude < 0.0001f)
            aim = Vector3.forward;
        aim.Normalize();
        var flat = new Vector3(aim.x, 0f, aim.z);
        if (flat.sqrMagnitude < 0.0001f)
            flat = Vector3.forward;
        // Raise the aim around the horizontal axis that is perpendicular to it.
        var axis = Vector3.Cross(Vector3.up, flat.normalized);
        var dir = Quaternion.AngleAxis(-THROW_PITCH_BONUS, axis) * aim;
        return dir.normalized * Mathf.Lerp(MIN_THROW_SPEED, MAX_THROW_SPEED, Mathf.Clamp01(charge));
    }

    /// <summary>Where a throw starts from (the held object's position).</summary>
    public Vector3 HeldPosition => transform.position;

    private void GetHoldPose(out Vector3 position, out Quaternion rotation)
    {
        var hand = PlayerRig.For(HolderObject.gameObject).RightHand;
        if (hand != null)
        {
            position = hand.position + HolderObject.transform.rotation * _handOffset;
            rotation = HolderObject.transform.rotation * Quaternion.Euler(_handEuler);
            return;
        }
        position = HolderObject.transform.TransformPoint(new Vector3(0.45f, 1.1f, 0.4f));
        rotation = HolderObject.transform.rotation;
    }

    private void FollowHolder()
    {
        GetHoldPose(out var position, out var rotation);
        transform.SetPositionAndRotation(position, rotation);
        _nrb.Rigidbody.position = position;
        _nrb.Rigidbody.rotation = rotation;
    }

    private void Release(Vector3 velocity)
    {
        CurrentHolder = PlayerRef.None;
        HolderObject = null;
        IsGrabbed = false;
        ApplyHeldState();

        _nrb.Rigidbody.WakeUp();
        _nrb.Rigidbody.linearVelocity = velocity;
        _nrb.Rigidbody.angularVelocity = velocity.sqrMagnitude > 1f ? Random.insideUnitSphere * 4f : Vector3.zero;
    }
}
