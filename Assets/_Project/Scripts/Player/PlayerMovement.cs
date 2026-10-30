using Fusion;
using UnityEngine;

/// <summary>
/// Shared contestant controller, simulated from networked input (predicted on the owner, authoritative on the host).
/// Tuned for party-game feel: quick acceleration and braking, real air control, coyote time, jump buffering, short
/// hops when Space is released early, a heavier fall, and physical knockbacks (<see cref="Launch"/>) that take control
/// away until you land. While winding up a throw the contestant turns to face the aim.
/// </summary>
[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Networked] public Vector3 CurrentVelocity { get; private set; }
    [Networked] public bool IsGrounded { get; private set; } = true;
    [Networked] public NetworkButtons PreviousButtons { get; private set; }
    /// <summary>Running while the player flies from a knockback; steering is weak meanwhile.</summary>
    [Networked] private TickTimer LaunchTimer { get; set; }
    [Networked] private int LastGroundedTick { get; set; }
    [Networked] private int JumpPressedTick { get; set; }
    [Networked] private NetworkBool JumpHeld { get; set; }

    [Header("Run")]
    [SerializeField] private float _maxSpeed = 7f;
    [SerializeField] private float _groundAcceleration = 70f;
    [SerializeField] private float _groundDeceleration = 90f;
    [SerializeField] private float _airAcceleration = 28f;
    [SerializeField] private float _rotationSpeed = 16f;

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 1.7f;
    [SerializeField] private float _gravity = 28f;
    [SerializeField, Tooltip("Gravity multiplier while falling, for a snappier arc")] private float _fallMultiplier = 1.6f;
    [SerializeField, Tooltip("Upward speed kept when Space is released early")] private float _shortHopCut = 0.5f;
    [SerializeField] private float _coyoteTime = 0.12f;
    [SerializeField] private float _jumpBufferTime = 0.15f;
    [SerializeField] private float _maxFallSpeed = 32f;

    [Header("Knockback")]
    [SerializeField] private float _launchDuration = 1.6f;
    [SerializeField, Tooltip("Share of air control kept while flying from a hit")] private float _launchControl = 0.25f;
    [SerializeField, Tooltip("Horizontal speed lost per second while flying")] private float _airDrag = 0.6f;

    private NetworkCharacterController _characterController;
    private CharacterController _unityController;
    private Player _player;
    private PlayerInteraction _interaction;

    public bool IsLaunched => !LaunchTimer.ExpiredOrNotRunning(Runner);

    /// <summary>Every contestant's controller on this peer; grabbable items ignore collisions with all of them.</summary>
    public static readonly System.Collections.Generic.List<CharacterController> Controllers = new();

    public override void Spawned()
    {
        if (!Controllers.Contains(_unityController)) Controllers.Add(_unityController);
        foreach (var item in GrabbableObject.Active)
            if (item != null) item.IgnorePlayers();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Controllers.Remove(_unityController);
    }

    private void Awake()
    {
        _characterController = GetComponent<NetworkCharacterController>();
        _unityController = GetComponent<CharacterController>();
        _player = GetComponent<Player>();
        _interaction = GetComponent<PlayerInteraction>();
    }

    /// <summary>
    /// Host only. Throws the player (explosions, hammers, shoves). Control is weak until they land again, so the
    /// hit reads clearly on every screen.
    /// </summary>
    public void Launch(Vector3 velocity)
    {
        CurrentVelocity = velocity;
        _characterController.Velocity = velocity;
        _characterController.IsGrounded = false;
        IsGrounded = false;
        LaunchTimer = TickTimer.CreateFromSeconds(Runner, _launchDuration);
    }

    public override void FixedUpdateNetwork()
    {
        float dt = Runner.DeltaTime;
        NetworkInputData data = default;
        bool hasInput = GetInput(out data);
        if (!hasInput || _player.ControlsLocked || _player.MovementLocked)
        {
            data.Direction = Vector3.zero;
            data.Buttons = default;
        }

        var velocity = CurrentVelocity;
        bool launched = IsLaunched;

        // Chaos modifiers are networked on the minigame, so prediction and the host apply the same rules.
        var chaos = MinigameController.Current != null && MinigameController.Current.Object != null && MinigameController.Current.Object.IsValid
            ? MinigameController.Current.ActiveChaos : ChaosModifier.None;
        float speedScale = chaos == ChaosModifier.SpeedRush ? 1.5f : chaos == ChaosModifier.SlowMotion ? 0.55f : chaos == ChaosModifier.Giants ? 1.15f : chaos == ChaosModifier.Tiny ? 0.85f : 1f;
        float gravityScale = chaos == ChaosModifier.LowGravity ? 0.4f : 1f;
        float jumpScale = chaos == ChaosModifier.SuperJump ? 1.9f : chaos == ChaosModifier.LowGravity ? 1.2f : 1f;
        bool ice = chaos == ChaosModifier.IceFloor;
        // Hot Potato: whoever carries the bomb runs a little faster, so passing it is a chase.
        if (MinigameController.Current is HotPotatoMinigameController potato && potato.Object != null && potato.Object.IsValid && potato.Holder == Object.InputAuthority)
            speedScale *= 1.12f;
        if (chaos == ChaosModifier.ReversedControls)
            data.Direction = -data.Direction;

        // Horizontal: accelerate toward the wanted velocity.
        Vector3 input = new Vector3(data.Direction.x, 0f, data.Direction.z);
        if (input.sqrMagnitude > 1f) input.Normalize();
        float speed = _maxSpeed * speedScale;
        if (_interaction != null && _interaction.HeldObject != null && _interaction.HeldObject.TryGetComponent<GrabbableObject>(out var carried))
            speed *= carried.CarrySpeed;
        Vector3 wish = Quaternion.Euler(0f, data.CameraRotation.eulerAngles.y, 0f) * input * speed;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float accel;
        if (launched) accel = _airAcceleration * _launchControl;
        else if (IsGrounded) accel = ice ? 9f : wish.sqrMagnitude > 0.01f ? _groundAcceleration : _groundDeceleration;
        else accel = _airAcceleration;
        if (launched)
            horizontal *= Mathf.Clamp01(1f - _airDrag * dt);
        // In the air without input, keep momentum instead of braking.
        if (launched || IsGrounded || wish.sqrMagnitude > 0.01f)
            horizontal = Vector3.MoveTowards(horizontal, launched ? horizontal + wish * 0.3f : wish, accel * dt);

        // Jump: buffered press + coyote time.
        var pressed = data.Buttons.GetPressed(PreviousButtons);
        bool jumpDown = data.Buttons.IsSet(InputButtons.Jump);
        if (pressed.IsSet(InputButtons.Jump))
            JumpPressedTick = Runner.Tick;
        PreviousButtons = data.Buttons;

        bool buffered = JumpPressedTick > 0 && (Runner.Tick - JumpPressedTick) * dt <= _jumpBufferTime;
        bool coyote = IsGrounded || (Runner.Tick - LastGroundedTick) * dt <= _coyoteTime;
        if (buffered && coyote && !launched && velocity.y <= 0.5f)
        {
            velocity.y = Mathf.Sqrt(2f * _gravity * gravityScale * _jumpHeight * jumpScale);
            JumpPressedTick = 0;
            LastGroundedTick = -1000;
            JumpHeld = true;
            IsGrounded = false;
        }

        // Variable height: releasing Space on the way up cuts the jump short.
        if (JumpHeld && !jumpDown)
        {
            JumpHeld = false;
            if (velocity.y > 0f && !launched)
                velocity.y *= _shortHopCut;
        }

        // Gravity, heavier on the way down.
        float g = _gravity * gravityScale * (velocity.y < 0f ? _fallMultiplier : 1f);
        velocity.y = Mathf.Max(velocity.y - g * dt, -_maxFallSpeed);
        if (IsGrounded && velocity.y < 0f)
            velocity.y = -2f;   // keep the controller pressed onto slopes and steps

        velocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        _unityController.Move(velocity * dt);

        bool grounded = _unityController.isGrounded && velocity.y <= 0.5f;
        if (grounded)
        {
            LastGroundedTick = Runner.Tick;
            if (velocity.y < 0f) velocity.y = -2f;
            if (launched && LaunchTimer.RemainingTime(Runner) < _launchDuration - 0.25f)
                LaunchTimer = TickTimer.None;
        }
        else if (_unityController.collisionFlags.HasFlag(CollisionFlags.Above) && velocity.y > 0f)
        {
            velocity.y = 0f;   // bonked a ceiling
        }
        IsGrounded = grounded;

        // Face the movement, or the aim while winding up a throw.
        Vector3 face = _interaction != null && _interaction.IsCharging ? data.LookDirection : new Vector3(velocity.x, 0f, velocity.z);
        face.y = 0f;
        if (face.sqrMagnitude > 0.04f && !launched)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), _rotationSpeed * dt);

        CurrentVelocity = velocity;
        _characterController.Velocity = velocity;
        _characterController.IsGrounded = grounded;
    }
}
