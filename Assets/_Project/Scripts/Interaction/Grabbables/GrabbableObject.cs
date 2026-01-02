using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

[RequireComponent(typeof(NetworkRigidbody3D))]
public class GrabbableObject : NetworkBehaviour, IGrabbable
{
    [Networked] public PlayerRef CurrentHolder { get; private set; } = PlayerRef.None;
    [Networked] public NetworkBool IsGrabbed { get; private set; }
    [Networked] private NetworkBool IsTargeted { get; set; }

    [SerializeField] private Transform visualRoot;
    [SerializeField] private float followSpeed = 20f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float positionThreshold = 0.01f;
    [SerializeField] private float scaleMultiplier = 1.1f;
    [SerializeField] private float releaseDelay = 0.2f; // Delay before re-enabling collisions
    [SerializeField] private float maxReleaseVelocity = 10f; // Clamp release velocity

    private NetworkRigidbody3D _rb;
    private Transform _targetTransform;
    private Vector3 _originalScale;
    private Transform _visualTransform;
    private Collider[] _colliders;
    private float _releaseTimer = 0f;

    public bool CanBeGrabbed => CurrentHolder == PlayerRef.None;

    public override void Spawned()
    {
        _rb = GetComponent<NetworkRigidbody3D>();

        // Get the visual transform
        _visualTransform = visualRoot != null ? visualRoot : transform;
        _originalScale = _visualTransform.localScale;

        // Cache all colliders on this object
        _colliders = GetComponentsInChildren<Collider>();
    }

    public override void Render()
    {
        // Update visual feedback every frame on all clients
        if (_visualTransform != null)
        {
            Vector3 targetScale = IsTargeted ? _originalScale * scaleMultiplier : _originalScale;
            _visualTransform.localScale = Vector3.Lerp(_visualTransform.localScale, targetScale, Time.deltaTime * 10f);
        }

        // Handle delayed collision re-enabling after release
        if (_releaseTimer > 0f)
        {
            _releaseTimer -= Time.deltaTime;
            if (_releaseTimer <= 0f)
            {
                _rb.Rigidbody.detectCollisions = true;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!IsGrabbed || _targetTransform == null) return;

        if (Object.HasStateAuthority)
        {
            // Calculate target position
            Vector3 targetPos = _targetTransform.position;
            Quaternion targetRot = _targetTransform.rotation;

            // Use MovePosition and MoveRotation for kinematic-like movement
            Vector3 newPos = Vector3.Lerp(
                _rb.Rigidbody.position,
                targetPos,
                followSpeed * Runner.DeltaTime
            );

            Quaternion newRot = Quaternion.Slerp(
                _rb.Rigidbody.rotation,
                targetRot,
                rotationSpeed * Runner.DeltaTime
            );

            // Apply movement through rigidbody
            _rb.Rigidbody.MovePosition(newPos);
            _rb.Rigidbody.MoveRotation(newRot);

            // Zero velocities to prevent drift
            _rb.Rigidbody.linearVelocity = Vector3.zero;
            _rb.Rigidbody.angularVelocity = Vector3.zero;
        }
    }

    public void Grab(PlayerRef player)
    {
        Debug.Log($"[Grab] Called by {player}, HasStateAuthority: {Object.HasStateAuthority}, CanBeGrabbed: {CanBeGrabbed}");

        if (!Object.HasStateAuthority) return;
        if (!CanBeGrabbed) return;

        CurrentHolder = player;
        IsGrabbed = true;

        // Find the player's interaction component to get ObjectHolder
        if (Runner.TryGetPlayerObject(player, out NetworkObject playerObj))
        {
            var interaction = playerObj.GetComponent<PlayerInteraction>();
            if (interaction != null)
            {
                _targetTransform = interaction.ObjectHolder;
                Debug.Log($"[Grab] Found ObjectHolder at {_targetTransform.position}");
            }
            else
            {
                Debug.LogWarning($"[Grab] No PlayerInteraction component found on player {player}");
            }
        }
        else
        {
            Debug.LogWarning($"[Grab] Failed to get player object for {player}");
        }

        // Disable collisions with all players
        _rb.Rigidbody.detectCollisions = false;

        // Configure rigidbody for being held
        if (_rb != null)
        {
            _rb.Rigidbody.useGravity = false;
            _rb.Rigidbody.isKinematic = false;
            _rb.Rigidbody.linearVelocity = Vector3.zero;
            _rb.Rigidbody.angularVelocity = Vector3.zero;

            // High drag for stability
            _rb.Rigidbody.linearDamping = 10f;
            _rb.Rigidbody.angularDamping = 10f;

            // Reduce mass to prevent physics explosions
            _rb.Rigidbody.mass = Mathf.Max(_rb.Rigidbody.mass * 0.5f, 0.1f);
        }

        Debug.Log($"[Grab] Successfully grabbed by {player}");
    }

    public void Release(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentHolder != player) return;

        CurrentHolder = PlayerRef.None;
        IsGrabbed = false;
        _targetTransform = null;

        // Re-enable physics
        if (_rb != null)
        {
            _rb.Rigidbody.useGravity = true;
            _rb.Rigidbody.linearDamping = 0f;
            _rb.Rigidbody.angularDamping = 0.05f;

            // Restore original mass (multiply by 2 to undo the 0.5 reduction)
            _rb.Rigidbody.mass = _rb.Rigidbody.mass * 2f;

            // Give the object velocity but clamp it to prevent launches
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObj))
            {
                var movement = playerObj.GetComponent<PlayerMovement>();
                if (movement != null)
                {
                    Vector3 releaseVel = movement.CurrentVelocity;

                    // Clamp the release velocity
                    if (releaseVel.magnitude > maxReleaseVelocity)
                    {
                        releaseVel = releaseVel.normalized * maxReleaseVelocity;
                    }

                    _rb.Rigidbody.linearVelocity = releaseVel;
                    Debug.Log($"[Release] Applied velocity: {releaseVel.magnitude:F2}");
                }
            }

            // Add small downward velocity to help it fall naturally
            _rb.Rigidbody.linearVelocity += Vector3.down * 0.5f;
        }

        // Delay re-enabling collisions with players to prevent launch bugs
        _releaseTimer = releaseDelay;
    }


    // Visual feedback methods - only called on input authority
    public void OnTargeted()
    {
        if (Object.HasInputAuthority)
            IsTargeted = true;
    }

    public void OnUntargeted()
    {
        if (Object.HasInputAuthority)
            IsTargeted = false;
    }
}