using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// Bomb Rush bomb. Rides a conveyor (host-driven) until it drops into the arena or someone grabs it; from then
/// the fuse burns. A thrown bomb is armed for a moment and goes off when it reaches another player.
/// Grabbing and throwing are handled by <see cref="GrabbableObject"/>.
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

    [SerializeField] private float _fuseSeconds = 7f;
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
    public void Initialize(BombRushMinigameController controller, int beltIndex)
    {
        _controller = controller;
        BeltIndex = beltIndex;
        BeltProgress = 0f;
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
        Fuse = TickTimer.CreateFromSeconds(Runner, _fuseSeconds);
    }

    private void Explode()
    {
        var holder = _grabbable.IsGrabbed ? _grabbable.CurrentHolder : PlayerRef.None;
        _controller.Explode(transform.position, holder);
        Runner.Despawn(Object);
    }

    public override void Render()
    {
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
        ShowCountdown(FuseRemaining);
    }

    private TMPro.TextMeshPro _countdown;
    private int _shownSeconds = -1;

    /// <summary>
    /// The last three seconds are counted down over the bomb with a tick (louder close by): playtesters could not
    /// tell when a bomb would go off.
    /// </summary>
    private void ShowCountdown(float seconds)
    {
        int whole = seconds > 3f ? -1 : Mathf.Max(1, Mathf.CeilToInt(seconds));
        if (_countdown == null)
        {
            var go = new GameObject("FuseCountdown");
            _countdown = go.AddComponent<TMPro.TextMeshPro>();
            _countdown.alignment = TMPro.TextAlignmentOptions.Center;
            _countdown.fontSize = 9f;
            _countdown.fontStyle = TMPro.FontStyles.Bold;
            _countdown.outlineWidth = 0.25f;
            _countdown.outlineColor = new Color32(40, 10, 10, 255);
            _countdown.rectTransform.sizeDelta = new Vector2(3f, 2f);
        }
        _countdown.gameObject.SetActive(whole > 0);
        if (whole <= 0) { _shownSeconds = -1; return; }
        var cam = Camera.main;
        _countdown.transform.position = transform.position + Vector3.up * 1.3f;
        if (cam != null) _countdown.transform.rotation = Quaternion.LookRotation(_countdown.transform.position - cam.transform.position);
        float beat = seconds - Mathf.Floor(seconds);
        _countdown.transform.localScale = Vector3.one * (1f + 0.35f * beat);
        _countdown.color = whole == 1 ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.79f, 0.24f);
        if (whole == _shownSeconds) return;
        _shownSeconds = whole;
        _countdown.text = whole.ToString();
        float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 10f;
        Sfx.Play(Sound.Tick, Mathf.Clamp01(1.2f - distance / 30f) * 0.8f, 0.05f);
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
        if (_countdown != null) Destroy(_countdown.gameObject);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
        ShowBlastRing(false, 0f);
        if (_countdown != null) _countdown.gameObject.SetActive(false);   // pooled: hidden until the next fuse
    }
}
