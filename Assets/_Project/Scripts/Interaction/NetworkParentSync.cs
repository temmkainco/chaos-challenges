using Fusion;
using UnityEngine;

public class NetworkParentSync : NetworkBehaviour
{
    private GrabbableObject _grabbable;
    private Transform _hand;

    private void Awake() => _grabbable = GetComponent<GrabbableObject>();

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !_grabbable.IsGrabbed) return;

        if (_hand == null) ResolveHand();
        if (_hand == null) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.MovePosition(_hand.TransformPoint(_grabbable._holdPositionOffset));
        rb.MoveRotation(_hand.rotation * Quaternion.Euler(_grabbable._holdRotationOffset));
    }

    private void ResolveHand()
    {
        if (_grabbable.HolderObject == null) return;
        var interaction = _grabbable.HolderObject.GetComponent<PlayerInteraction>();
        if (interaction != null) _hand = interaction.GetHandTransform();
    }

    public override void Render()
    {
        if (!_grabbable.IsGrabbed)
        {
            _hand = null;
            return;
        }

        if (_hand == null) ResolveHand();

        if (_hand != null)
        {
            transform.position = _hand.TransformPoint(_grabbable._holdPositionOffset);
            transform.rotation = _hand.rotation * Quaternion.Euler(_grabbable._holdRotationOffset);
        }
    }
}