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
    /// <summary>
    /// The follow camera looks down at the contestant (about 17 degrees at rest), so its aim is lifted by this much:
    /// the resting camera throws a little upward, looking up lobs higher and farther, looking down throws flat.
    /// </summary>
    public const float AIM_LIFT = 30f;
    public const float MIN_ELEVATION = 6f, MAX_ELEVATION = 60f;

    [Networked, OnChangedRender(nameof(OnGrabbedChanged))]
    public NetworkBool IsGrabbed { get; private set; }

    public bool CanBeInteractedWith => Object != null && Object.IsValid && !IsGrabbed;
    public Outline Outline { get; private set; }

    [Networked] public PlayerRef CurrentHolder { get; private set; }
    [Networked] public NetworkObject HolderObject { get; private set; }

    public enum HoldStyle { Front, Hand }

    [Header("Hold Settings")]
    [SerializeField, Tooltip("Front: carried in front of the chest, clear of the body (bombs, prizes). Hand: in the right hand (keys).")]
    private HoldStyle _holdStyle = HoldStyle.Front;
    [SerializeField, Tooltip("Offset from the right hand bone, in the holder's space (x right, y up, z forward)")] private Vector3 _handOffset = new Vector3(0.05f, -0.05f, 0.18f);
    [SerializeField] private Vector3 _handEuler;
    [SerializeField, Tooltip("Off for items that may only be carried and dropped (Key Hunt keys)")] private bool _canThrow = true;

    [SerializeField, Range(0.3f, 1f), Tooltip("Holder's speed multiplier while carrying this (heavy prizes slow you down)")] private float _carrySpeed = 1f;

    public bool CanThrow => _canThrow;

    /// <summary>Live grabbables on this peer; players never collide with them (see <see cref="IgnorePlayers"/>).</summary>
    public static readonly System.Collections.Generic.List<GrabbableObject> Active = new();
    public float CarrySpeed => _carrySpeed;

    private NetworkRigidbody3D _nrb;
    private Collider _collider;
    private Transform _interpolationTarget;
    private float _halfSize = 0.4f;


    private void Awake()
    {
        _nrb = GetComponent<NetworkRigidbody3D>();
        _collider = GetComponent<Collider>();
        _interpolationTarget = FindInterpolationTarget();
        if (_collider != null)
            _halfSize = Mathf.Max(_collider.bounds.extents.x, _collider.bounds.extents.z);
        // Models from prop packs can carry their own Rigidbody: it would drop the model out of the item (tiny falling bombs).
        foreach (var body in GetComponentsInChildren<Rigidbody>(true))
            if (body.gameObject != gameObject)
            {
                body.isKinematic = true;
                Destroy(body);
            }

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
        if (!Active.Contains(this)) Active.Add(this);
        IgnorePlayers();
        ApplyHeldState();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
    }

    /// <summary>
    /// Items never push, block or trap contestants: players walk through them, and a released or thrown item can
    /// never wedge inside its holder. Grabbing uses overlap queries, so it is unaffected.
    /// </summary>
    public void IgnorePlayers()
    {
        if (_collider == null) return;
        foreach (var controller in PlayerMovement.Controllers)
            if (controller != null)
                Physics.IgnoreCollision(_collider, controller, true);
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
            // The rigidbody's interpolation moves the visual child; while held it must sit exactly in the hand.
            if (_interpolationTarget != null)
            {
                _interpolationTarget.localPosition = Vector3.zero;
                _interpolationTarget.localRotation = Quaternion.identity;
            }
        }
    }

    private Transform FindInterpolationTarget()
    {
        // Walk up the generic base types until the field is found (NetworkRigidbody<,> -> NetworkRigidbodyBase).
        for (var type = typeof(NetworkRigidbody3D); type != null; type = type.BaseType)
        {
            var field = type.GetField("_interpolationTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                return field.GetValue(_nrb) as Transform;
        }
        return null;
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
        // Launch angle from the camera pitch (see AIM_LIFT), around the horizontal axis perpendicular to the aim.
        float pitch = Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
        float elevation = Mathf.Clamp(pitch + AIM_LIFT, MIN_ELEVATION, MAX_ELEVATION);
        var axis = Vector3.Cross(Vector3.up, flat.normalized);
        var dir = Quaternion.AngleAxis(-elevation, axis) * flat.normalized;
        return dir.normalized * Mathf.Lerp(MIN_THROW_SPEED, MAX_THROW_SPEED, Mathf.Clamp01(charge));
    }

    /// <summary>Where a throw starts from (the held object's position).</summary>
    public Vector3 HeldPosition => transform.position;

    private void GetHoldPose(out Vector3 position, out Quaternion rotation)
    {
        var holder = HolderObject.transform;
        if (_holdStyle == HoldStyle.Front)
        {
            // In front of the chest, far enough out for the item's own size so it never clips the body.
            float reach = 0.45f + _halfSize;
            position = holder.position + Vector3.up * 1.1f + holder.forward * reach;
            rotation = holder.rotation;
            return;
        }
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
