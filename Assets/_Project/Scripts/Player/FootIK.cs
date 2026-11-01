using UnityEngine;

/// <summary>
/// Plants the feet on uneven ground while a contestant stands (slopes, steps, the edge of a platform) and lowers the
/// hips to the lower foot, so neither foot floats or sinks. Runs in the animator's IK pass (base layer); moving feet are
/// left to the animation.
/// </summary>
[RequireComponent(typeof(Animator))]
public class FootIK : MonoBehaviour
{
    [SerializeField, Tooltip("Above this speed (m/s) the feet follow the animation only")]
    private float _maxSpeed = 0.5f;
    [SerializeField, Tooltip("How far above and below the animated foot to look for ground (m)")]
    private float _reach = 0.35f;
    [SerializeField, Tooltip("How fast the feet blend onto the ground and back (per second)")]
    private float _blendSpeed = 8f;
    [SerializeField, Tooltip("What counts as ground (not contestants, not the invisible play-area walls)")]
    private LayerMask _ground = ~((1 << ArenaProps.PLAYER_LAYER) | (1 << ArenaProps.FENCE_LAYER));

    private Animator _animator;
    private PlayerMovement _movement;
    private float _weight, _hipsDrop;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _movement = GetComponentInParent<PlayerMovement>();
    }

    private void OnAnimatorIK(int layer)
    {
        if (layer != 0 || _movement == null || _movement.Object == null || !_movement.Object.IsValid) return;
        var v = _movement.CurrentVelocity;
        bool standing = _movement.IsGrounded && new Vector2(v.x, v.z).magnitude < _maxSpeed;
        _weight = Mathf.MoveTowards(_weight, standing ? 1f : 0f, _blendSpeed * Time.deltaTime);
        if (_weight <= 0f)
        {
            _hipsDrop = 0f;
            return;
        }
        float lowest = 0f;
        Plant(AvatarIKGoal.LeftFoot, _animator.leftFeetBottomHeight, ref lowest);
        Plant(AvatarIKGoal.RightFoot, _animator.rightFeetBottomHeight, ref lowest);
        _hipsDrop = Mathf.Lerp(_hipsDrop, lowest, 1f - Mathf.Exp(-12f * Time.deltaTime));
        _animator.bodyPosition += Vector3.up * (_hipsDrop * _weight);
    }

    private void Plant(AvatarIKGoal foot, float soleHeight, ref float lowest)
    {
        var animated = _animator.GetIKPosition(foot);
        if (!Physics.Raycast(animated + Vector3.up * _reach, Vector3.down, out var hit, _reach * 2f, _ground, QueryTriggerInteraction.Ignore))
            return;
        var target = hit.point + Vector3.up * soleHeight;
        lowest = Mathf.Min(lowest, target.y - animated.y);
        _animator.SetIKPositionWeight(foot, _weight);
        _animator.SetIKPosition(foot, target);
        _animator.SetIKRotationWeight(foot, _weight);
        _animator.SetIKRotation(foot, Quaternion.FromToRotation(Vector3.up, hit.normal) * _animator.GetIKRotation(foot));
    }
}
