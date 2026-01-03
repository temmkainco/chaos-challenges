using Fusion;
using UnityEngine;

/// <summary>
/// Add this component to GrabbableObject to ensure parenting syncs correctly across network
/// </summary>
[RequireComponent(typeof(GrabbableObject))]
public class NetworkParentSync : NetworkBehaviour
{
    private GrabbableObject _grabbable;
    private bool _wasGrabbedLastFrame;

    private void Awake()
    {
        _grabbable = GetComponent<GrabbableObject>();
    }

    public override void Render()
    {
        // Detect state change and re-parent on all clients
        if (_grabbable.IsGrabbed != _wasGrabbedLastFrame)
        {
            if (_grabbable.IsGrabbed)
            {
                // Object was just grabbed - find the holder and parent to their hand
                var holder = FindPlayerWithRef(_grabbable.CurrentHolder);
                if (holder != null)
                {
                    var handTransform = holder.GetHandTransform();
                    if (handTransform != null)
                    {
                        Debug.Log($"[NetworkParentSync] Re-parenting to {holder.name}'s hand on client");
                        // Force re-parent on this client
                        ForceParent(handTransform);
                    }
                }
            }
            else
            {
                // Object was just released
                Debug.Log($"[NetworkParentSync] Un-parenting on client");
                ForceUnparent();
            }

            _wasGrabbedLastFrame = _grabbable.IsGrabbed;
        }
    }

    private void ForceParent(Transform handTransform)
    {
        var rb = GetComponent<Rigidbody>();
        var col = GetComponent<Collider>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (col != null)
        {
            col.enabled = false;
        }

        // Store world scale before parenting
        Vector3 worldScale = transform.lossyScale;

        transform.SetParent(handTransform);

        // Use the offsets from GrabbableObject if available
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Calculate and apply local scale to maintain world scale
        Vector3 targetLocalScale = new Vector3(
            worldScale.x / handTransform.lossyScale.x,
            worldScale.y / handTransform.lossyScale.y,
            worldScale.z / handTransform.lossyScale.z
        );
        transform.localScale = targetLocalScale;
    }

    private void ForceUnparent()
    {
        transform.SetParent(null);

        var rb = GetComponent<Rigidbody>();
        var col = GetComponent<Collider>();

        if (rb != null)
        {
            rb.isKinematic = false;
        }

        if (col != null)
        {
            col.enabled = true;
        }
    }

    private PlayerInteraction FindPlayerWithRef(PlayerRef playerRef)
    {
        if (playerRef == PlayerRef.None) return null;

        var players = FindObjectsByType<PlayerInteraction>(sortMode: FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.Object != null && player.Object.InputAuthority == playerRef)
            {
                return player;
            }
        }
        return null;
    }
}