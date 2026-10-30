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
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Active.Remove(this);
    }
}
