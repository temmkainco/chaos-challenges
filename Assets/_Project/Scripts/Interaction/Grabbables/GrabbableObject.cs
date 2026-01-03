using Fusion;
using UnityEngine;

public class GrabbableObject : NetworkBehaviour, IGrabbable
{
    [Networked] public PlayerRef CurrentHolder { get; private set; }
    [Networked] public NetworkBool IsGrabbed { get; private set; }

    private Rigidbody _rigidbody;
    private Collider _collider;
    private Transform _originalParent;
    private Vector3 _originalScale;

    [SerializeField] private Vector3 _holdPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 _holdRotationOffset = Vector3.zero;

    public bool CanBeGrabbed => !IsGrabbed;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _originalParent = transform.parent;
        _originalScale = transform.localScale;
    }

    public void Grab(PlayerRef player, Transform handTransform)
    {
        if (!Object.HasStateAuthority) return;
        if (IsGrabbed) return;

        CurrentHolder = player;
        IsGrabbed = true;

        ParentToHand(handTransform);
    }

    public void Release(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;
        if (!IsGrabbed || CurrentHolder != player) return;

        CurrentHolder = PlayerRef.None;
        IsGrabbed = false;

        UnparentFromHand();
    }

    private void ParentToHand(Transform handTransform)
    {
        if (handTransform == null) return;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (_collider != null)
        {
            _collider.enabled = false;
        }

        Vector3 worldScale = transform.lossyScale;

        transform.SetParent(handTransform);

        transform.localPosition = _holdPositionOffset;
        transform.localRotation = Quaternion.Euler(_holdRotationOffset);

        Vector3 targetLocalScale = new Vector3(
            worldScale.x / handTransform.lossyScale.x,
            worldScale.y / handTransform.lossyScale.y,
            worldScale.z / handTransform.lossyScale.z
        );
        transform.localScale = targetLocalScale;
    }

    private void UnparentFromHand()
    {
        transform.SetParent(_originalParent);
        transform.localScale = _originalScale;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
        }

        if (_collider != null)
        {
            _collider.enabled = true;
        }
    }

    public override void Spawned()
    {
        if (IsGrabbed && CurrentHolder != PlayerRef.None)
        {
            var holder = FindPlayerWithRef(CurrentHolder);
            if (holder != null)
            {
                var handTransform = holder.GetHandTransform();
                if (handTransform != null)
                {
                    ParentToHand(handTransform);
                }
            }
        }
    }

    private PlayerInteraction FindPlayerWithRef(PlayerRef playerRef)
    {
        var players = FindObjectsByType<PlayerInteraction>(sortMode: FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.Object.InputAuthority == playerRef)
            {
                return player;
            }
        }
        return null;
    }
}