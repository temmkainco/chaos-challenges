using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// Bomb Rush bomb. Rides a conveyor (host-driven) until it drops into the arena or someone grabs it; from then
/// the fuse burns. A thrown bomb is armed for a moment and goes off when it reaches another player.
/// Grabbing and throwing are handled by <see cref="GrabbableObject"/>. Dropped bombs (island supply, the sky) float
/// down slowly; a lit bomb shows its fuse burning by pulsing faster and redder (no numbers: the fuse length varies a
/// little, so you know it is soon but not exactly when); a bomb glows in its owner's colour (whose island it landed on,
/// then who carried it last).
/// </summary>
[RequireComponent(typeof(GrabbableObject))]
public class Bomb : NetworkBehaviour
{
    /// <summary>Live instances on this peer (no scene searches in the simulation).</summary>
    public static readonly List<Bomb> Active = new();

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    [Networked] public TickTimer Fuse { get; private set; }
    [Networked] public NetworkBool Lit { get; private set; }
    [Networked] private int BeltIndex { get; set; }
    [Networked] private float BeltProgress { get; set; }
    [Networked] private TickTimer ArmedTimer { get; set; }
    [Networked] private PlayerRef Thrower { get; set; }
    /// <summary>Whose bomb it is: the island's player for a supply bomb, then whoever carried it last (its glow).</summary>
    [Networked] public PlayerRef Owner { get; private set; }
    /// <summary>Dropped (supply or sky) and not thrown yet: falls no faster than <see cref="_dropSpeed"/>.</summary>
    [Networked] private NetworkBool Dropping { get; set; }

    [SerializeField] private float _fuseSeconds = 7f;
    [SerializeField, Range(0f, 0.5f), Tooltip("Each fuse is this much longer or shorter at random, so it can't be timed exactly")]
    private float _fuseJitter = 0.2f;
    [SerializeField, Tooltip("Fastest a dropped bomb (island supply, Bomb Bonanza) falls, metres per second")]
    private float _dropSpeed = 4f;
    [SerializeField, Tooltip("Reach of the glow in the owner's colour (metres)")]
    private float _glowRange = 3.5f;
    [SerializeField] private float _armedSeconds = 1.5f;
    [SerializeField] private float _contactRadius = 1.1f;
    [SerializeField] private Transform _visual;
    [SerializeField] private Renderer[] _bodyRenderers;
    [SerializeField] private Transform _spark;

    private GrabbableObject _grabbable;
    private NetworkRigidbody3D _body;
    private BombRushMinigameController _controller;
    private bool _wasGrabbed;
    private PlayerRef _lastHolder;
    private MaterialPropertyBlock _block;
    private Vector3 _visualScale;

    private const float MIN_FUSE_ON_PICKUP = 3f;
    /// <summary>Dropped from the sky by Bomb Bonanza rather than riding a conveyor.</summary>
    [Networked] public NetworkBool FromSky { get; set; }

    public float FuseRemaining => Fuse.RemainingTime(Runner) ?? _fuseSeconds;

    private void Awake()
    {
        _grabbable = GetComponent<GrabbableObject>();
        _body = GetComponent<NetworkRigidbody3D>();
        _block = new MaterialPropertyBlock();
        if (_visual != null) _visualScale = _visual.localScale;
    }

    /// <summary>Host only, right after spawning.</summary>
    public void Initialize(BombRushMinigameController controller, int beltIndex, PlayerRef owner = default)
    {
        Owner = owner;
        Thrower = PlayerRef.None;
        Dropping = beltIndex < 0;
        _controller = controller;
        BeltIndex = beltIndex;
        BeltProgress = 0f;
        // Bombs come from a pool: a recycled one must not come back lit. Supply bombs only light when picked up.
        Lit = false;
        Fuse = TickTimer.None;
        ArmedTimer = TickTimer.None;
        FromSky = false;
    }

    public override void Spawned()
    {
        if (!Active.Contains(this)) Active.Add(this);
        _controller ??= FindFirstObjectByType<BombRushMinigameController>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _controller == null)
            return;

        TrackThrows();

        if (BeltIndex >= 0)
            RideBelt();
        else if (FromSky && !Lit && !_grabbable.IsGrabbed)
            LeaveBelt(Vector3.down * 3f);   // dropped from the sky (Bomb Bonanza): falls lit
        else if (!FromSky && !Lit && !_grabbable.IsGrabbed && _body.Rigidbody.isKinematic)
        {
            // An island's supply bomb: drops onto the island and waits, unlit, to be picked up.
            _body.Rigidbody.isKinematic = false;
            _body.Rigidbody.useGravity = true;
        }

        // Dropped bombs float down slowly, so you see where they land.
        var body = _body.Rigidbody;
        if (Dropping && !body.isKinematic && body.linearVelocity.y < -_dropSpeed)
            body.linearVelocity = new Vector3(body.linearVelocity.x, -_dropSpeed, body.linearVelocity.z);

        if (Lit && Fuse.Expired(Runner))
        {
            Explode();
            return;
        }

        if (!ArmedTimer.ExpiredOrNotRunning(Runner) && !_grabbable.IsGrabbed
            && _controller.AnyPlayerWithin(transform.position, _contactRadius, Thrower))
        {
            Explode();
        }
    }

    private void TrackThrows()
    {
        bool grabbed = _grabbable.IsGrabbed;
        if (grabbed)
        {
            if (!_wasGrabbed)
            {
                // Picked up: always at least a few seconds to throw it (a bomb lying around used to blow in your hands).
                if (Lit && (Fuse.RemainingTime(Runner) ?? 0f) < MIN_FUSE_ON_PICKUP)
                    Fuse = TickTimer.CreateFromSeconds(Runner, MIN_FUSE_ON_PICKUP);
            }
            _lastHolder = _grabbable.CurrentHolder;
            Owner = _lastHolder;
            Dropping = false;
            if (BeltIndex >= 0) LeaveBelt(Vector3.zero);
            Light();
        }
        else if (_wasGrabbed)
        {
            // Just released: thrown by the last holder.
            Thrower = _lastHolder;
            ArmedTimer = TickTimer.CreateFromSeconds(Runner, _armedSeconds);
        }
        _wasGrabbed = grabbed;
    }

    private void RideBelt()
    {
        var belt = _controller.GetBelt(BeltIndex);
        if (belt == null)
        {
            LeaveBelt(Vector3.zero);
            return;
        }

        BeltProgress += belt.Speed * Runner.DeltaTime / Mathf.Max(0.1f, belt.Length);
        var rb = _body.Rigidbody;
        rb.isKinematic = true;
        var position = belt.Evaluate(Mathf.Min(BeltProgress, 1f));
        transform.position = position;
        rb.position = position;

        if (BeltProgress >= 1f)
            LeaveBelt(belt.Direction * belt.Speed + Vector3.up);
    }

    private void LeaveBelt(Vector3 velocity)
    {
        BeltIndex = -1;
        if (_grabbable.IsGrabbed)
            return;

        var rb = _body.Rigidbody;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = velocity;
        Light();
    }

    private void Light()
    {
        if (Lit) return;
        Lit = true;
        Fuse = TickTimer.CreateFromSeconds(Runner, _fuseSeconds * (1f + Random.Range(-_fuseJitter, _fuseJitter)));
    }

    private void Explode()
    {
        var holder = _grabbable.IsGrabbed ? _grabbable.CurrentHolder : PlayerRef.None;
        _controller.Explode(transform.position, holder);
        Runner.Despawn(Object);
    }

    public override void Render()
    {
        _grabbable.Blocked = FromSky;   // sky bombs are a hazard, not ammo
        ShowOwnerGlow();
        if (_visual == null)
            return;

        if (!Lit)
        {
            _visual.localScale = _visualScale;
            ShowBlastRing(false, 0f);
            return;
        }

        // Pulse faster and redder as the fuse burns down.
        float remaining = Mathf.Clamp01(FuseRemaining / _fuseSeconds);
        float frequency = Mathf.Lerp(9f, 1.5f, remaining);
        float pulse = (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) + 1f) * 0.5f;
        _visual.localScale = _visualScale * (1f + pulse * Mathf.Lerp(0.18f, 0.05f, remaining));

        var color = Color.Lerp(new Color(0.35f, 0.38f, 0.45f), new Color(1f, 0.2f, 0.15f), pulse * (1f - remaining * 0.6f));
        foreach (var r in _bodyRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, color);
            r.SetPropertyBlock(_block);
        }

        if (_spark != null)
            _spark.localScale = Vector3.one * (0.6f + Random.value * 0.6f) * 0.25f;
        ShowBlastRing(true, 1f - remaining);
        // The last couple of seconds tick faster and louder (instead of the old numbers).
        if (remaining < 0.35f && Time.time >= _nextTick)
        {
            _nextTick = Time.time + Mathf.Lerp(0.12f, 0.45f, remaining / 0.35f);
            var cam = Camera.main;
            float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 10f;
            Sfx.Play(Sound.Tick, Mathf.Clamp01(1.2f - distance / 30f) * 0.6f, 0.05f);
        }
    }

    private float _nextTick;
    private Light _glow;
    private PlayerRef _glowFor = PlayerRef.None;

    /// <summary>A light in the owner's colour, so everyone can see whose bomb is whose.</summary>
    private void ShowOwnerGlow()
    {
        if (_glow == null)
        {
            var go = new GameObject("OwnerGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.6f;
            _glow = go.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.shadows = LightShadows.None;
            _glow.intensity = 3f;
        }
        bool show = Owner != PlayerRef.None && !FromSky;
        if (_glow.enabled != show) _glow.enabled = show;
        if (!show || Owner == _glowFor) return;
        _glowFor = Owner;
        var match = NetworkGameManager.Instance;
        _glow.color = match != null && match.TryGetPlayerColor(Owner, out var color) ? color : Color.white;
        _glow.range = _glowRange;
        if (_spark != null && _spark.TryGetComponent<Renderer>(out var spark))
        {
            spark.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, _glow.color);
            spark.SetPropertyBlock(_block);
        }
    }

    private Transform _blastRing;
    private Material _blastMaterial;

    /// <summary>
    /// A ring on the ground under a lit bomb marking the knock-out radius (<see cref="BombRushMinigameController.KILL_RADIUS"/>),
    /// getting stronger as the fuse burns down, so everyone can see what the blast will catch.
    /// </summary>
    private void ShowBlastRing(bool show, float urgency)
    {
        if (!show)
        {
            if (_blastRing != null && _blastRing.gameObject.activeSelf) _blastRing.gameObject.SetActive(false);
            return;
        }
        if (_blastRing == null)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = "BlastRadius";
            _blastRing = ring.transform;
            _blastRing.localScale = new Vector3(BombRushMinigameController.KILL_RADIUS * 2f, 0.01f, BombRushMinigameController.KILL_RADIUS * 2f);
            _blastMaterial = new Material(Resources.Load<Material>("ThrowArc"));
            var renderer = ring.GetComponent<Renderer>();
            renderer.sharedMaterial = _blastMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        if (!_blastRing.gameObject.activeSelf) _blastRing.gameObject.SetActive(true);
        var origin = transform.position;
        _blastRing.position = Physics.Raycast(origin + Vector3.up * 0.2f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point + Vector3.up * 0.03f
            : origin;
        float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 18f, urgency));
        _blastMaterial.color = new Color(1f, 0.25f, 0.2f, Mathf.Lerp(0.1f, 0.35f, urgency) * (0.6f + 0.4f * blink));
    }

    private void OnDestroy()
    {
        if (_blastRing != null) Destroy(_blastRing.gameObject);
        if (_blastMaterial != null) Destroy(_blastMaterial);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
        ShowBlastRing(false, 0f);
    }
}
