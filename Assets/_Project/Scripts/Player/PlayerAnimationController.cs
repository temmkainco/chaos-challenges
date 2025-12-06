using Fusion;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : NetworkBehaviour
{
    [SerializeField] private PlayerMovement movement;
    private Animator _animator;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int JumpParam = Animator.StringToHash("Jump");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        float speed = movement.CurrentVelocity.magnitude;

        _animator.SetFloat(SpeedParam, speed);
        _animator.SetBool(JumpParam, !movement.IsGrounded);
    }
}
