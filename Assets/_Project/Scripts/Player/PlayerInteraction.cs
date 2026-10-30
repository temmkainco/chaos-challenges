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

    public IInteractable CurrentTarget { get; private set; }
    public bool IsCharging => ChargeStartTick > 0 && HeldObject != null;
    public float Charge => IsCharging ? Mathf.Clamp01((Runner.Tick - ChargeStartTick) * Runner.DeltaTime / FULL_CHARGE_TIME) : 0f;

    [SerializeField] private float _distance = 3f;
    [SerializeField, Tooltip("Radius of the grab zone in front of the player")] private float _reach = 1.6f;
    [SerializeField] private LayerMask _mask;
    [Header("Shove")]
    [SerializeField] private float _shoveRange = 1.8f;
    [SerializeField] private float _shovePush = 9f;
    [SerializeField] private float _shoveLift = 3.5f;
    [SerializeField] private float _shoveCooldown = 0.9f;
    [SerializeField] private Transform _eyesPoint;

    private Player _player;
    private readonly FocusHighlighter _focusHighlighter = new();
    private readonly Collider[] _overlap = new Collider[16];

    public override void Spawned()
    {
        _player = GetComponent<Player>();

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
        if (HeldObject != null || _player.ControlsLocked || _player.IsSpectator)
        {
            _focusHighlighter.Clear();
            CurrentTarget = null;
            HighlightShoveTarget(null);
            return;
        }

        CurrentTarget = FindTarget(Physics.defaultPhysicsScene, _player.Camera.transform.forward);
        _focusHighlighter.Highlight(CurrentTarget);
        HighlightShoveTarget(CurrentTarget == null ? FindShoveTarget(_player.Camera.transform.forward) : null);
    }

    private Transform _shoveMarker;

    /// <summary>
    /// Local player: a red ring at the feet of the contestant your shove would hit (empty hands, shoving allowed).
    /// A ring rather than a mesh outline: outlining a character rewrites its shared mesh.
    /// </summary>
    private void HighlightShoveTarget(Player target)
    {
        var minigame = MinigameController.Current;
        if (minigame == null || !minigame.IsGameActive || !minigame.AllowShove || _player.IsSpectator) target = null;
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

    /// <summary>Same pick as the host's shove: the closest contestant within reach in front.</summary>
    private Player FindShoveTarget(Vector3 look)
    {
        var flat = new Vector3(look.x, 0f, look.z);
        flat = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
        int count = Physics.OverlapSphereNonAlloc(transform.position + flat * (_shoveRange * 0.6f) + Vector3.up, _shoveRange, _overlap, ~0, QueryTriggerInteraction.Ignore);
        Player best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var other = _overlap[i].GetComponentInParent<Player>();
            if (other == null || other == _player || other.Object == null || other.ControlsLocked || other.IsSpectator) continue;
            var to = other.transform.position - transform.position;
            to.y = 0f;
            if (Vector3.Angle(flat, to) > 70f || to.magnitude >= bestDistance) continue;
            best = other;
            bestDistance = to.magnitude;
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
            ClearStaleHeldObject();

        if (!GetInput(out NetworkInputData input)) return;
        // Spectators (knocked out) can't grab, throw or shove anything.
        if (_player.ControlsLocked || _player.IsSpectator)
            input.Buttons = default;

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

    /// <summary>
    /// Empty-handed throw button: a push with both arms (the swing is predicted for the owner so it animates
    /// instantly); the host shoves the closest contestant in front. A minigame can use the button instead
    /// (Hot Potato: the bomb holder hands the bomb over).
    /// </summary>
    private void TryShove()
    {
        var minigame = MinigameController.Current;
        if (HasStateAuthority && minigame != null && minigame.IsGameActive && minigame.HandleShove(_player))
            return;
        if (!ShoveCooldown.ExpiredOrNotRunning(Runner) || (minigame != null && (!minigame.AllowShove || !minigame.IsGameActive)))
            return;
        ShoveCooldown = TickTimer.CreateFromSeconds(Runner, _shoveCooldown);
        ShoveTick = Runner.Tick;
        if (!HasStateAuthority)
            return;

        var flat = new Vector3(LookDirection.x, 0f, LookDirection.z);
        flat = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
        int count = Runner.GetPhysicsScene().OverlapSphere(transform.position + flat * (_shoveRange * 0.6f) + Vector3.up, _shoveRange, _overlap, ~0, QueryTriggerInteraction.Ignore);
        Player best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var other = _overlap[i].GetComponentInParent<Player>();
            if (other == null || other == _player || other.Object == null || other.ControlsLocked || other.IsSpectator) continue;
            var to = other.transform.position - transform.position;
            to.y = 0f;
            if (Vector3.Angle(flat, to) > 70f || to.magnitude >= bestDistance) continue;
            best = other;
            bestDistance = to.magnitude;
        }
        if (best == null)
            return;

        var push = (best.transform.position - transform.position).WithY(0f).normalized;
        if (best.TryGetComponent<PlayerMovement>(out var movement))
        {
            float strength = minigame != null ? minigame.ShoveStrength : 1f;
            movement.Launch(push * _shovePush * strength + Vector3.up * _shoveLift);
        }
        if ((minigame == null || minigame.ShoveDropsHeldItem) && best.Interaction != null)
            best.Interaction.ForceDrop();
        RPC_Shoved(best.transform.position);
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
        Sfx.Play(Sound.Whoosh, 1f, 0.15f);
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
