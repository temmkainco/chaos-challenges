using Fusion;
using UnityEngine;

/// <summary>
/// Drives the player's animator from the networked movement state, the same on every peer: locomotion speed (the blend
/// tree's thresholds are the clips' own speeds on this character, so the feet don't slide; faster than the sprint clip
/// plays it faster), grounded, take-off, being launched (hit reaction) and the shove (<see cref="PlayerInteraction.ShoveTick"/>).
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : NetworkBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField, Tooltip("How fast the sprint clip moves this character (m/s); moving faster plays it faster")]
    private float _sprintClipSpeed = 6.378f;
    [SerializeField, Tooltip("Upward speed (m/s) on leaving the ground that counts as a jump, not stepping off a ledge")]
    private float _takeOffSpeed = 2f;
    [SerializeField, Tooltip("Seconds a jump, hit or shove trigger waits to be used before it is dropped")]
    private float _triggerLife = 0.25f;

    private Animator _animator;
    private PlayerInteraction _interaction;
    private bool _wasGrounded = true, _wasLaunched;
    private int _shownShoveTick;
    private readonly float[] _triggerSetAt = { -1f, -1f, -1f };

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int LocoScaleParam = Animator.StringToHash("LocoScale");
    private static readonly int GroundedParam = Animator.StringToHash("Grounded");
    private static readonly int[] Triggers = { Animator.StringToHash("Jump"), Animator.StringToHash("Hit"), Animator.StringToHash("Shove") };
    private const int JUMP = 0, HIT = 1, SHOVE = 2;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _interaction = GetComponentInParent<PlayerInteraction>();
    }

    private void Update()
    {
        if (movement == null || movement.Object == null || !movement.Object.IsValid) return;
        var v = movement.CurrentVelocity;
        float speed = new Vector2(v.x, v.z).magnitude;
        bool grounded = movement.IsGrounded, launched = movement.IsLaunched;

        _animator.SetFloat(SpeedParam, speed);
        _animator.SetFloat(LocoScaleParam, Mathf.Max(1f, speed / _sprintClipSpeed));
        _animator.SetBool(GroundedParam, grounded);

        if (_wasGrounded && !grounded && v.y > _takeOffSpeed && !launched) Fire(JUMP);
        if (launched && !_wasLaunched) Fire(HIT);
        if (_interaction != null && _interaction.ShoveTick != _shownShoveTick)
        {
            _shownShoveTick = _interaction.ShoveTick;
            if ((Runner.Tick - _shownShoveTick) * Runner.DeltaTime < _triggerLife) Fire(SHOVE);
        }
        _wasGrounded = grounded;
        _wasLaunched = launched;

        // A trigger nobody used (a jump while already falling) must not fire later.
        for (int i = 0; i < Triggers.Length; i++)
            if (_triggerSetAt[i] >= 0f && Time.time - _triggerSetAt[i] > _triggerLife)
            {
                _animator.ResetTrigger(Triggers[i]);
                _triggerSetAt[i] = -1f;
            }
    }

    private void Fire(int trigger)
    {
        _animator.SetTrigger(Triggers[trigger]);
        _triggerSetAt[trigger] = Time.time;
    }
}
