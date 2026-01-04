using UnityEngine;

public class PlayerNccCollisionHandler : MonoBehaviour
{
    [Header("Push Settings")]
    [SerializeField] private float _pushPower = 2.0f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        if (body == null || body.isKinematic) return;

        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        body.linearVelocity = pushDir * _pushPower;
    }
}
