using Fusion;
using UnityEngine;

/// <summary>
/// Interaction runs from the networked input: the host picks the target from the player's position and
/// <see cref="NetworkInputData.LookDirection"/> and applies the result, so every client sees the same outcome.
/// E grabs the best target in front of the player (or drops what is held); holding the throw button winds up a throw
/// aimed with the camera, releasing it throws. The owning client adds the focus outline and instant local feedback.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    /// <summary>Seconds of wind-up for a full-power throw.</summary>
    public const float FULL_CHARGE_TIME = 0.9f;

    [Networked] public NetworkObject HeldObject { get; private set; }
    /// <summary>Last aim direction from input; throws use it.</summary>
    [Networked] public Vector3 LookDirection { get; private set; }
    /// <summary>Tick the current wind-up started on, 0 when not charging.</summary>
    [Networked] public int ChargeStartTick { get; private set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] private TickTimer ShoveCooldown { get; set; }
    /// <summary>Tick of the last shove swing (predicted for the owner): drives the push animation on every peer.</summary>
    [Networked] public int ShoveTick { get; private set; }
    /// <summary>Set by the host on whoever was just shoved: no shove lands on them until it runs out.</summary>
    [Networked] private TickTimer ShoveImmunity { get; set; }
    public bool IsShoveImmune => !ShoveImmunity.ExpiredOrNotRunning(Runner);

    public IInteractable CurrentTarget { get; private set; }
    public bool IsCharging => ChargeStartTick > 0 && HeldObject != null;
    public float Charge => IsCharging ? Mathf.Clamp01((Runner.Tick - ChargeStartTick) * Runner.DeltaTime / FULL_CHARGE_TIME) : 0f;

    [SerializeField] private float _distance = 3f;
    [SerializeField, Tooltip("Radius of the grab zone in front of the player")] private float _reach = 1.6f;
    [SerializeField] private LayerMask _mask;
    [SerializeField] private Transform _eyesPoint;

    private Player _player;
    private PlayerMovement _movement;
    private readonly FocusHighlighter _focusHighlighter = new();
    private readonly Collider[] _overlap = new Collider[16];

    public override void Spawned()
    {
        _player = GetComponent<Player>();
        _movement = GetComponent<PlayerMovement>();

        if (Object.HasInputAuthority)
            gameObject.AddComponent<ThrowPreview>();

        if (!Object.HasStateAuthority && !Object.HasInputAuthority)
            return;

        Runner.SetPlayerObject(Object.InputAuthority, Object);
    }

    void Update()
    {
        if (Object == null || !Object.HasInputAuthority) return;
        UpdateLocalTarget();
    }

    private void UpdateLocalTarget()
    {
        if (HeldObject != null || _player.ControlsLocked)
        {
            _focusHighlighter.Clear();
            CurrentTarget = null;
            HighlightShoveTarget(null);
            return;
        }
        if (_player.IsSpectator)
        {
            _focusHighlighter.Clear();
            CurrentTarget = null;
            HighlightShoveTarget(FindShoveTarget(false));
            return;
        }

        CurrentTarget = FindTarget(Physics.defaultPhysicsScene, _player.Camera.transform.forward);
        _focusHighlighter.Highlight(CurrentTarget);
        HighlightShoveTarget(CurrentTarget == null && ShoveCooldown.ExpiredOrNotRunning(Runner) ? FindShoveTarget(false) : null);
    }

    private Transform _shoveMarker;

    /// <summary>
    /// Local player: a red ring at the feet of the contestant your shove would hit (empty hands, shoving allowed).
    /// A ring rather than a mesh outline: outlining a character rewrites its shared mesh.
    /// </summary>
    private void HighlightShoveTarget(Player target)
    {
        var minigame = MinigameController.Current;
        if (minigame == null || !minigame.IsGameActive || (!minigame.AllowShove && !_player.IsSpectator)) target = null;
        if (target == null)
        {
            if (_shoveMarker != null && _shoveMarker.gameObject.activeSelf) _shoveMarker.gameObject.SetActive(false);
            return;
        }
        if (_shoveMarker == null)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = "ShoveTarget";
            _shoveMarker = ring.transform;
            _shoveMarker.localScale = new Vector3(1.3f, 0.01f, 1.3f);
            var renderer = ring.GetComponent<Renderer>();
            renderer.material = new Material(Resources.Load<Material>("ThrowArc")) { color = new Color(1f, 0.3f, 0.28f, 0.55f) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (!_shoveMarker.gameObject.activeSelf) _shoveMarker.gameObject.SetActive(true);
        _shoveMarker.position = target.transform.position + Vector3.up * 0.05f;
    }

    private void OnDestroy()
    {
        if (_shoveMarker != null) Destroy(_shoveMarker.gameObject);
    }

    /// <summary>
    /// The contestant a shove from here would hit, the same way on every peer: within <see cref="PushConfig.Range"/>
    /// and <see cref="PushConfig.ConeAngle"/> of the character's facing (never the camera), in line of sight; the
    /// smallest angle wins, the nearer one when angles are about equal. <paramref name="seenByShover"/>: the host
    /// measures where the shover saw each contestant (<see cref="Rewind"/>), the owner's ring where they are now.
    /// </summary>
    private Player FindShoveTarget(bool seenByShover)
    {
        var config = PushConfig.Instance;
        var forward = transform.forward.WithY(0f).normalized;
        var origin = transform.position;
        Player best = null;
        float bestAngle = float.MaxValue, bestDistance = float.MaxValue;
        foreach (var controller in PlayerMovement.Controllers)
        {
            var other = controller != null ? controller.GetComponent<Player>() : null;
            if (other == null || other == _player || other.Object == null || !other.Object.IsValid || other.ControlsLocked
                || other.IsSpectator != _player.IsSpectator || (other.Interaction != null && other.Interaction.IsShoveImmune))
                continue;
            var position = seenByShover ? Rewind.PositionSeenBy(_player, other) : other.transform.position;
            var to = position - origin;
            if (Mathf.Abs(to.y) > config.MaxHeightDifference) continue;
            to.y = 0f;
            float distance = to.magnitude - controller.radius;
            float angle = to.sqrMagnitude > 1e-4f ? Vector3.Angle(forward, to) : 0f;
            if (distance > config.Range || angle > config.ConeAngle) continue;
            if (Physics.Linecast(origin + Vector3.up, position + Vector3.up, config.LineOfSight, QueryTriggerInteraction.Ignore)) continue;
            bool better = angle < bestAngle - config.AngleTie || (Mathf.Abs(angle - bestAngle) <= config.AngleTie && distance < bestDistance);
            if (best == null || better)
            {
                best = other;
                bestAngle = angle;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>
    /// What the player is reaching for: whatever the aim ray hits first, otherwise the interactable in the grab zone
    /// in front of the player that is closest to the aim. Third-person aiming at small items is fiddly, so the zone
    /// does most of the work.
    /// </summary>
    private IInteractable FindTarget(PhysicsScene physics, Vector3 look)
    {
        if (physics.Raycast(_eyesPoint.position, look, out var hit, _distance, _mask))
        {
            var direct = hit.collider.GetComponentInParent<IInteractable>();
            if (direct != null && direct.CanBeInteractedWith)
                return direct;
        }

        var flat = new Vector3(look.x, 0f, look.z);
        flat = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
        var center = transform.position + flat * (_reach * 0.8f) + Vector3.up * 0.8f;
        int count = physics.OverlapSphere(center, _reach, _overlap, _mask, QueryTriggerInteraction.Collide);

        IInteractable best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var interactable = _overlap[i].GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanBeInteractedWith || interactable is not Component component)
                continue;

            var to = component.transform.position - transform.position;
            to.y = 0f;
            float score = to.magnitude * 0.5f + Vector3.Angle(flat, to) / 45f;
            if (score < bestScore)
            {
                bestScore = score;
                best = interactable;
            }
        }
        return best;
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            ClearStaleHeldObject();
            // The swing's contact frame: the push lands now, not on the press.
            if (ShoveTick > 0 && Runner.Tick == ShoveTick + ShoveContactTicks)
                LandShove();
        }

        if (!GetInput(out NetworkInputData input)) return;
        // Ghosts (knocked out) can't grab or throw; their shove only reaches other ghosts.
        if (_player.ControlsLocked)
            input.Buttons = default;
        else if (_player.IsSpectator)
        {
            bool shove = input.Buttons.IsSet(InputButtons.Throw);
            input.Buttons = default;
            input.Buttons.Set(InputButtons.Throw, shove);
        }

        bool interactPressed = input.Buttons.WasPressed(PreviousButtons, InputButtons.Interact);
        bool throwPressed = input.Buttons.WasPressed(PreviousButtons, InputButtons.Throw);
        bool throwReleased = input.Buttons.WasReleased(PreviousButtons, InputButtons.Throw);
        PreviousButtons = input.Buttons;

        if (input.LookDirection.sqrMagnitude > 0.01f)
            LookDirection = input.LookDirection.normalized;

        // Wind-up is predicted on the owner so the arc preview responds instantly.
        if (HeldObject == null)
        {
            ChargeStartTick = 0;
            if (throwPressed)
                TryShove();
        }
        else if (throwPressed && (!HeldObject.TryGetComponent<GrabbableObject>(out var held) || held.CanThrow))
            ChargeStartTick = Runner.Tick;

        if (throwReleased && IsCharging)
        {
            float charge = Charge;
            ChargeStartTick = 0;
            if (HasStateAuthority && HeldObject.TryGetComponent<GrabbableObject>(out var grabbable))
            {
                grabbable.Throw(Object.InputAuthority, LookDirection, charge);
                HeldObject = null;
            }
            return;
        }

        if (!interactPressed)
            return;

        // E while winding up cancels the throw instead of dropping.
        if (IsCharging)
        {
            ChargeStartTick = 0;
            return;
        }

        if (Object.HasInputAuthority && Runner.IsForward && HeldObject == null)
            TryLocalInteraction();

        if (!HasStateAuthority)
            return;

        var minigame = MinigameController.Current;
        if (minigame != null && minigame.HandlePrimaryAction(_player))
            return;

        if (HeldObject != null)
            ReleaseHeldObject();
        else
            InteractWithTarget();
    }

    /// <summary>Ticks from the press to the swing's contact frame (<see cref="PushConfig.ContactDelay"/>).</summary>
    private int ShoveContactTicks => Mathf.Max(1, Mathf.RoundToInt(PushConfig.Instance.ContactDelay / Runner.DeltaTime));

    private bool CanShove(MinigameController minigame) =>
        !_player.ControlsLocked && (_movement == null || !_movement.IsLaunched)
        // Ghosts may always shove each other (it can't touch the game).
        && (minigame == null || ((minigame.AllowShove || _player.IsSpectator) && minigame.IsGameActive));

    /// <summary>
    /// Empty-handed throw button: starts a two-handed push (the swing is predicted for the owner so it animates
    /// instantly). The host lands it at the contact frame (<see cref="LandShove"/>). A minigame can use the button
    /// instead (Hot Potato: the bomb holder hands the bomb over; Tail Tag: a grab), straight away.
    /// </summary>
    private void TryShove()
    {
        var minigame = MinigameController.Current;
        if (HasStateAuthority && !_player.IsSpectator && minigame != null && minigame.IsGameActive && minigame.HandleShove(_player))
            return;
        // No shoving while knocked about (stunned) or during the cooldown.
        if (!ShoveCooldown.ExpiredOrNotRunning(Runner) || !CanShove(minigame))
            return;
        ShoveCooldown = TickTimer.CreateFromSeconds(Runner, PushConfig.Instance.Cooldown);
        ShoveTick = Runner.Tick;
    }

    /// <summary>
    /// Host, at the swing's contact frame: the best contestant in the cone in front of the shover (as the shover saw
    /// them) is pushed along the shover's facing, loses control briefly and can't be shoved again for a moment.
    /// Nothing happens if the shover was knocked about or the round stopped during the wind-up.
    /// </summary>
    private void LandShove()
    {
        var minigame = MinigameController.Current;
        if (!CanShove(minigame)) return;
        var target = FindShoveTarget(true);
        if (target == null) return;

        var config = PushConfig.Instance;
        var push = transform.forward.WithY(0f).normalized;
        if (target.TryGetComponent<PlayerMovement>(out var movement))
        {
            float strength = minigame != null ? minigame.ShoveStrength : 1f;
            movement.Launch(push * config.Force * strength + Vector3.up * config.Lift, config.ControlLoss);
        }
        if (target.Interaction != null)
        {
            target.Interaction.ShoveImmunity = TickTimer.CreateFromSeconds(Runner, config.Immunity);
            if (minigame == null || minigame.ShoveDropsHeldItem) target.Interaction.ForceDrop();
        }
        RPC_Shoved(target.transform.position);
    }

    /// <summary>Scene view: every contestant's shove reach and cone (<see cref="PushConfig.DrawGizmos"/>).</summary>
    private void OnDrawGizmos()
    {
        var config = PushConfig.Instance;
        if (!config.DrawGizmos) return;
        bool ready = Object == null || !Object.IsValid || ShoveCooldown.ExpiredOrNotRunning(Runner);
        Gizmos.color = ready ? new Color(1f, 0.35f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
        var origin = transform.position + Vector3.up * 0.05f;
        var forward = transform.forward.WithY(0f).normalized;
        float radius = TryGetComponent<CharacterController>(out var own) ? own.radius : 0.4f;
        float reach = config.Range + radius;   // a target's centre can be this far (its body at Range)
        const int SEGMENTS = 16;
        var previous = origin + Quaternion.Euler(0f, -config.ConeAngle, 0f) * forward * reach;
        Gizmos.DrawLine(origin, previous);
        for (int i = 1; i <= SEGMENTS; i++)
        {
            var next = origin + Quaternion.Euler(0f, Mathf.Lerp(-config.ConeAngle, config.ConeAngle, i / (float)SEGMENTS), 0f) * forward * reach;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.DrawLine(origin, previous);
    }

    /// <summary>Host only: let go of whatever is held (shoves, hits).</summary>
    public void ForceDrop()
    {
        if (!HasStateAuthority || HeldObject == null) return;
        ChargeStartTick = 0;
        ReleaseHeldObject();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Shoved(Vector3 position)
    {
        Sfx.Play(Sound.Pop, 0.9f, 0.15f);
        WorldPopup.Show(position + Vector3.up * 2.6f, "OOF!", new Color(1f, 0.79f, 0.24f), 6f);
    }

    private void TryLocalInteraction()
    {
        if (CurrentTarget == null || !CurrentTarget.CanBeInteractedWith || CurrentTarget is not ILocalInteractable localInteractable)
            return;

        localInteractable.LocalInteract();
    }

    /// <summary>Host only.</summary>
    private void InteractWithTarget()
    {
        var interactable = FindTarget(Runner.GetPhysicsScene(), LookDirection);
        if (interactable == null)
            return;

        interactable.Interact(Object.InputAuthority, Object);

        if (interactable is ICancellableInteractable && interactable is NetworkBehaviour networkBehaviour)
            HeldObject = networkBehaviour.Object;
    }

    /// <summary>Host only.</summary>
    private void ReleaseHeldObject()
    {
        if (HeldObject.TryGetComponent<ICancellableInteractable>(out var cancellable))
            cancellable.CancelInteraction(Object.InputAuthority, Object);

        HeldObject = null;
    }

    /// <summary>Host only. The object may have been despawned or dropped without us.</summary>
    private void ClearStaleHeldObject()
    {
        if (HeldObject == null)
            return;

        if (!HeldObject.TryGetComponent<IGrabbable>(out var grabbable) || grabbable.CurrentHolder != Object.InputAuthority)
            HeldObject = null;
    }
}
