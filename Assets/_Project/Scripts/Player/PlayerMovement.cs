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
    /// <summary>Plane nose angle (degrees, up positive) and wing bank, for flying and the rider's pose.</summary>
    [Networked] public float Pitch { get; private set; }
    [Networked] public float Bank { get; private set; }
    /// <summary>Speed pad / turbo: vehicles go faster while it runs.</summary>
    [Networked] private TickTimer BoostTimer { get; set; }

    [Header("Run")]
    [SerializeField] private float _maxSpeed = 7f;
    [SerializeField] private float _groundAcceleration = 70f;
    [SerializeField] private float _groundDeceleration = 90f;
    [SerializeField] private float _airAcceleration = 28f;
    [SerializeField] private float _rotationSpeed = 16f;

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 1.7f;
    private const float SPRINT = 1.3f;
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
    public bool IsBoosted => !BoostTimer.ExpiredOrNotRunning(Runner);

    private const float KART_SPEED = 19f, KART_TURN = 130f, PLANE_SPEED = 17f, PLANE_TURN = 80f;

    /// <summary>Host only. A speed pad or turbo for vehicles.</summary>
    public void Boost(float seconds) => BoostTimer = TickTimer.CreateFromSeconds(Runner, seconds);

    /// <summary>Host only. Faces <paramref name="yaw"/> and stops (respawning vehicles on the track).</summary>
    public void ResetVehicle(float yaw)
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        CurrentVelocity = Vector3.zero;
        _characterController.Velocity = Vector3.zero;
        LaunchTimer = TickTimer.None;
        Pitch = 0f;
        Bank = 0f;
    }

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
        var minigame = MinigameController.Current != null && MinigameController.Current.Object != null && MinigameController.Current.Object.IsValid
            ? MinigameController.Current : null;
        var chaos = minigame != null ? minigame.ActiveChaos : ChaosModifier.None;
        float speedScale = chaos == ChaosModifier.SpeedRush ? 1.5f : chaos == ChaosModifier.SlowMotion ? 0.55f : chaos == ChaosModifier.Giants ? 1.15f : chaos == ChaosModifier.Tiny ? 0.85f : 1f;
        float gravityScale = chaos == ChaosModifier.LowGravity ? 0.4f : 1f;
        float jumpScale = chaos == ChaosModifier.SuperJump ? 1.9f : chaos == ChaosModifier.LowGravity ? 1.2f : 1f;
        bool ice = chaos == ChaosModifier.IceFloor;
        // Hot Potato: whoever carries the bomb runs faster, so passing it is a chase.
        if (MinigameController.Current is HotPotatoMinigameController potato && potato.Object != null && potato.Object.IsValid && potato.Holder == Object.InputAuthority)
            speedScale *= 1.25f;
        if (chaos == ChaosModifier.ReversedControls)
            data.Direction = -data.Direction;

        var vehicle = minigame != null && !_player.IsSpectator ? minigame.Vehicle : VehicleKind.None;
        if (vehicle == VehicleKind.Kart) { Drive(data, dt, speedScale, gravityScale, ice, minigame); return; }
        if (vehicle == VehicleKind.Plane) { Fly(data, dt, speedScale, minigame); return; }

        // Horizontal: accelerate toward the wanted velocity.
        Vector3 input = new Vector3(data.Direction.x, 0f, data.Direction.z);
        if (input.sqrMagnitude > 1f) input.Normalize();
        // Shift: sprint (a bit faster, not while carrying - carry speed still applies on top).
        if (data.Buttons.IsSet(InputButtons.Sprint) && (MinigameController.Current == null || MinigameController.Current.AllowsSprint))
            speedScale *= SPRINT;
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
        // A twist's wind drifts everyone (not stored in the velocity, so it stops the moment the twist ends).
        var wind = minigame != null && !_player.ControlsLocked ? minigame.ChaosWind : Vector3.zero;
        // A moving floor (rolling log) carries whoever stands on it.
        if (minigame != null && IsGrounded) wind += minigame.GroundDrift(transform.position);
        _unityController.Move((velocity + wind) * dt);

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
    /// <summary>
    /// Kart: W/S throttle and brake, A/D steer (sharper the faster you go), tyres grip sideways, Space hops.
    /// Knockbacks still throw the kart around.
    /// </summary>
    private void Drive(NetworkInputData data, float dt, float speedScale, float gravityScale, bool ice, MinigameController minigame)
    {
        float throttle = Mathf.Clamp(data.Direction.z, -1f, 1f), steer = Mathf.Clamp(data.Direction.x, -1f, 1f);
        var velocity = CurrentVelocity;
        bool launched = IsLaunched;
        var flat = new Vector3(velocity.x, 0f, velocity.z);
        var fwd = transform.forward;
        float forwardSpeed = Vector3.Dot(flat, fwd);
        float top = KART_SPEED * speedScale * (IsBoosted ? 1.45f : 1f);
        if (!launched)
        {
            float target = throttle >= 0f ? throttle * top : throttle * top * 0.45f;
            bool braking = Mathf.Abs(target) < Mathf.Abs(forwardSpeed) || (target != 0f && Mathf.Sign(target) != Mathf.Sign(forwardSpeed));
            float rate = throttle == 0f ? 7f : braking ? 28f : 15f;
            if (IsBoosted && throttle > 0f) forwardSpeed = Mathf.Max(forwardSpeed, top * 0.85f);
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, target, rate * dt);
            float grip = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 6f) * (forwardSpeed < 0f ? -1f : 1f);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + steer * KART_TURN * grip * dt, 0f);
            fwd = transform.forward;
            var lateral = flat - Vector3.Project(flat, fwd);
            lateral = Vector3.MoveTowards(lateral, Vector3.zero, (ice ? 4f : 45f) * dt);
            flat = fwd * forwardSpeed + lateral;
        }
        else flat *= Mathf.Clamp01(1f - _airDrag * dt);

        var pressed = data.Buttons.GetPressed(PreviousButtons);
        PreviousButtons = data.Buttons;
        if (pressed.IsSet(InputButtons.Jump) && IsGrounded && !launched)
        {
            velocity.y = Mathf.Sqrt(2f * _gravity * gravityScale * 0.5f);
            IsGrounded = false;
        }
        float g = _gravity * gravityScale * (velocity.y < 0f ? _fallMultiplier : 1f);
        velocity.y = Mathf.Max(velocity.y - g * dt, -_maxFallSpeed);
        if (IsGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity = new Vector3(flat.x, velocity.y, flat.z);
        var wind = !_player.ControlsLocked ? minigame.ChaosWind : Vector3.zero;
        _unityController.Move((velocity + wind) * dt);
        // Barriers bounce the kart back off them (instead of letting it grind along or stick).
        if (_unityController.collisionFlags.HasFlag(CollisionFlags.Sides))
        {
            var flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
            var origin = transform.position + Vector3.up * 0.6f;
            if (flatVelocity.sqrMagnitude > 1f && Runner.GetPhysicsScene().SphereCast(origin, 0.35f, flatVelocity.normalized, out var hit, 1.2f,
                    ~(1 << ArenaProps.PLAYER_LAYER), QueryTriggerInteraction.Ignore))
            {
                var normal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                if (Vector3.Dot(flatVelocity, normal) < 0f)
                {
                    var bounced = Vector3.Reflect(flatVelocity, normal) * 0.55f + normal * 2f;
                    velocity = new Vector3(bounced.x, velocity.y, bounced.z);
                }
            }
            else velocity = new Vector3(velocity.x * 0.6f, velocity.y, velocity.z * 0.6f);
        }
        bool grounded = _unityController.isGrounded && velocity.y <= 0.5f;
        if (grounded)
        {
            if (velocity.y < 0f) velocity.y = -2f;
            if (launched && LaunchTimer.RemainingTime(Runner) < _launchDuration - 0.25f) LaunchTimer = TickTimer.None;
        }
        IsGrounded = grounded;
        CurrentVelocity = velocity;
        _characterController.Velocity = velocity;
        _characterController.IsGrounded = grounded;
        Bank = Mathf.MoveTowards(Bank, -steer * Mathf.Clamp01(Mathf.Abs(forwardSpeed) / top) * 8f, 60f * dt);
    }

    /// <summary>
    /// Plane: always flying forward; W/S nose up and down, A/D turn (the plane banks), Shift boosts. No gravity.
    /// Parked (no movement) while controls are locked.
    /// </summary>
    private void Fly(NetworkInputData data, float dt, float speedScale, MinigameController minigame)
    {
        float climb = Mathf.Clamp(data.Direction.z, -1f, 1f), steer = Mathf.Clamp(data.Direction.x, -1f, 1f);
        PreviousButtons = data.Buttons;
        if (_player.ControlsLocked || _player.MovementLocked)
        {
            _unityController.Move(Vector3.down * 2f * dt);
            CurrentVelocity = Vector3.zero;
            _characterController.Velocity = Vector3.zero;
            Pitch = Mathf.MoveTowards(Pitch, 0f, 60f * dt);
            Bank = Mathf.MoveTowards(Bank, 0f, 60f * dt);
            return;
        }
        Pitch = Mathf.MoveTowards(Pitch, climb * 35f, 90f * dt);
        Bank = Mathf.MoveTowards(Bank, -steer * 35f, 120f * dt);
        if (transform.position.y > minigame.MaxAltitude && Pitch > -10f) Pitch = Mathf.MoveTowards(Pitch, -10f, 120f * dt);
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + steer * PLANE_TURN * dt, 0f);
        float speed = PLANE_SPEED * speedScale * (IsBoosted || data.Buttons.IsSet(InputButtons.Sprint) ? 1.35f : 1f);
        var velocity = Quaternion.Euler(-Pitch, transform.eulerAngles.y, 0f) * Vector3.forward * speed;
        if (IsLaunched) velocity = Vector3.Lerp(velocity, CurrentVelocity, 0.9f) + Vector3.down * 9f * dt;
        _unityController.Move((velocity + minigame.ChaosWind) * dt);
        IsGrounded = _unityController.isGrounded;
        CurrentVelocity = velocity;
        _characterController.Velocity = velocity;
        _characterController.IsGrounded = IsGrounded;
    }
}
