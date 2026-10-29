using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Networked] public Vector3 CurrentVelocity { get; private set; }
    [Networked] public bool IsGrounded { get; private set; } = true;
    [Networked] public NetworkButtons PreviousButtons { get; private set; }
    /// <summary>Running while the player flies from a knockback; movement input is ignored meanwhile.</summary>
    [Networked] private TickTimer LaunchTimer { get; set; }

    [SerializeField] private float _movementSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;
    [SerializeField] private float _launchDuration = 1.6f;
    [SerializeField, Tooltip("Horizontal speed lost per second while flying")] private float _airDrag = 0.6f;

    private NetworkCharacterController _characterController;
    private CharacterController _unityController;
    private Player _player;

    private void Awake()
    {
        _characterController = GetComponent<NetworkCharacterController>();
        _unityController = GetComponent<CharacterController>();
        _player = GetComponent<Player>();
    }

    /// <summary>
    /// Host only. Throws the player (explosions, knockback). The flight is ballistic for a moment, because the
    /// character controller's braking would otherwise cancel the push within a few ticks.
    /// </summary>
    public void Launch(Vector3 velocity)
    {
        _characterController.Velocity = velocity;
        _characterController.IsGrounded = false;
        IsGrounded = false;
        LaunchTimer = TickTimer.CreateFromSeconds(Runner, _launchDuration);
    }

    public override void FixedUpdateNetwork()
    {
        if (!LaunchTimer.ExpiredOrNotRunning(Runner))
        {
            Fly();
            return;
        }

        if (!GetInput(out NetworkInputData data))
        {
            if (Object.HasStateAuthority)
                CurrentVelocity = Vector3.zero;

            return;
        }

        // Still run the controller so gravity applies, just without player intent.
        if (_player.ControlsLocked || _player.MovementLocked)
        {
            data.Direction = Vector3.zero;
            data.Buttons = default;
        }

        Vector3 inputDirection = new Vector3(data.Direction.x, 0f, data.Direction.z);
        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        Quaternion flatCameraRotation = Quaternion.Euler(0f, data.CameraRotation.eulerAngles.y, 0f);
        Vector3 targetDir = flatCameraRotation * inputDirection;
        Vector3 velocity = targetDir * _movementSpeed;

        _characterController.Move(velocity);

        if (targetDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDir);
            _characterController.transform.rotation = Quaternion.Slerp(
                _characterController.transform.rotation,
                targetRotation,
                _rotationSpeed * Runner.DeltaTime
            );
        }

        var pressed = data.Buttons.GetPressed(PreviousButtons);
        if (pressed.WasPressed(PreviousButtons, InputButtons.Jump) && IsGrounded)
        {
            _characterController.Jump();
            IsGrounded = false;
        }

        if (!IsGrounded && _characterController.IsGrounded && _characterController.Velocity.y <= 0f)
        {
            IsGrounded = true;
        }

        PreviousButtons = data.Buttons;

        if (Object.HasStateAuthority)
        {
            CurrentVelocity = velocity;
        }
    }

    private void Fly()
    {
        float dt = Runner.DeltaTime;
        var velocity = _characterController.Velocity;
        velocity.y += _characterController.gravity * dt;
        float drag = Mathf.Clamp01(1f - _airDrag * dt);
        velocity.x *= drag;
        velocity.z *= drag;

        _unityController.Move(velocity * dt);
        _characterController.Velocity = velocity;

        // Landed after the initial lift-off: hand control back.
        float elapsed = _launchDuration - (LaunchTimer.RemainingTime(Runner) ?? 0f);
        if (elapsed > 0.25f && _unityController.isGrounded && velocity.y <= 0f)
        {
            LaunchTimer = TickTimer.None;
            _characterController.Velocity = new Vector3(velocity.x, 0f, velocity.z);
        }
    }
}
