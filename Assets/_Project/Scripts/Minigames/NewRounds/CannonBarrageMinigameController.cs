using DG.Tweening;
using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cannon Barrage: cannons around the stage fire cannonballs straight across it, more and faster as time goes on.
/// A hit knocks you flying; off the stage and you are out. Watch the cannons, they glow before they fire.
/// Last contestant standing wins; survivors tie at the bell.
/// Chaos: METEOR SHOWER on top of the barrage, SLOW MOTION makes dodging harder; its own twist, BROADSIDE,
/// fires every cannon but two at once.
/// </summary>
public class CannonBarrageMinigameController : EliminationMinigameController
{
    private const float BALL_SPEED = 13f, BALL_RADIUS = 0.75f, WARNING = 0.8f;

    [SerializeField] private Transform _centre;
    [SerializeField] private Material _material;
    [SerializeField] private float _arenaRadius = 21f;
    [SerializeField] private int _cannons = 8;
    [SerializeField, Tooltip("Cannon model (Quaternius, CC0)")] private GameObject _cannonModel;

    [Networked] private TickTimer NextVolley { get; set; }

    public override string TwistTitle => "BROADSIDE";
    public override string TwistDescription => "Every cannon but two fires at once - find the gap!";

    private readonly List<(int cannon, int tick)> _balls = new();
    private Transform[] _cannonModels;
    private readonly List<(Transform ball, int cannon, int tick)> _visualBalls = new();

    protected override float OutHeight => _centre.position.y - 4f;

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
        return _centre.position + Vector3.up * 0.5f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 6f;
    }

    private Vector3 CannonPosition(int i)
    {
        float angle = i / (float)_cannons * Mathf.PI * 2f;
        return _centre.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _arenaRadius + Vector3.up * 0.9f;
    }

    /// <summary>Each cannon fires slightly off-centre so lanes vary between volleys but stay predictable.</summary>
    private Vector3 Direction(int i, int tick)
    {
        var toCentre = (_centre.position + Vector3.up * 0.9f - CannonPosition(i)).WithY(0f).normalized;
        float wobble = ((tick * 7919 + i * 104729) % 41 - 20) * 0.6f;
        return Quaternion.Euler(0f, wobble, 0f) * toCentre;
    }

    private Vector3 BallPosition(int cannon, int tick, float now) =>
        CannonPosition(cannon) + Direction(cannon, tick) * BALL_SPEED * Mathf.Max(0f, now - tick * Runner.DeltaTime - WARNING);

    public override string GetStatusText() => IsGameActive ? $"{_alive.Count} still dodging" : null;

    public override void Spawned()
    {
        base.Spawned();
        var model = _cannonModel;
        _cannonModels = new Transform[_cannons];
        for (int i = 0; i < _cannons; i++)
        {
            Transform cannon;
            if (model != null)
            {
                cannon = Instantiate(model, transform).transform;
                var renderers = cannon.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    cannon.localScale *= 2.6f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                }
            }
            else
            {
                cannon = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                Destroy(cannon.GetComponent<Collider>());
                cannon.SetParent(transform, false);
                cannon.localScale = new Vector3(1.2f, 1f, 1.2f);
            }
            cannon.position = CannonPosition(i) - Vector3.up * 0.6f;
            cannon.rotation = Quaternion.LookRotation((_centre.position - cannon.position).WithY(0f));
            foreach (var c in cannon.GetComponentsInChildren<Collider>()) Destroy(c);
            _cannonModels[i] = cannon;
        }
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;

        if (NextVolley.ExpiredOrNotRunning(Runner))
        {
            float elapsed = Definition.GameDuration * MatchSettings.LengthMultiplier - TimeRemaining;
            float interval = TwistActive ? 2.6f : Mathf.Lerp(2.2f, 0.9f, Mathf.Clamp01(elapsed / 50f));
            NextVolley = TickTimer.CreateFromSeconds(Runner, interval);
            if (TwistActive)
            {
                // Broadside: all cannons except two neighbours (the gap to run for).
                int gap = Random.Range(0, _cannons);
                for (int cannon = 0; cannon < _cannons; cannon++)
                {
                    if (cannon == gap || cannon == (gap + 1) % _cannons) continue;
                    _balls.Add((cannon, Runner.Tick));
                    RPC_Fire(cannon, Runner.Tick);
                }
            }
            else
            {
                int shots = 1 + Mathf.Min(3, (int)(elapsed / 15f));
                for (int s = 0; s < shots; s++)
                {
                    int cannon = Random.Range(0, _cannons);
                    _balls.Add((cannon, Runner.Tick));
                    RPC_Fire(cannon, Runner.Tick);
                }
            }
        }

        float now = Runner.Tick * Runner.DeltaTime;
        for (int b = _balls.Count - 1; b >= 0; b--)
        {
            var (cannon, tick) = _balls[b];
            var pos = BallPosition(cannon, tick, now);
            if ((pos - CannonPosition(cannon)).magnitude > _arenaRadius * 2.2f) { _balls.RemoveAt(b); continue; }
            foreach (var who in _alive)
            {
                if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) continue;
                var offset = player.transform.position + Vector3.up * 0.9f - pos;
                if (offset.magnitude > BALL_RADIUS + 0.5f) continue;
                if (player.TryGetComponent<PlayerMovement>(out var movement) && !movement.IsLaunched)
                    movement.Launch(Direction(cannon, tick) * 16f + Vector3.up * 7f);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Fire(int cannon, int tick)
    {
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        Destroy(ball.GetComponent<Collider>());
        ball.localScale = Vector3.one * BALL_RADIUS * 2f;
        var renderer = ball.GetComponent<Renderer>();
        renderer.sharedMaterial = _material;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(0.18f, 0.16f, 0.22f));
        renderer.SetPropertyBlock(block);
        ball.position = CannonPosition(cannon);
        _visualBalls.Add((ball, cannon, tick));
        if (_cannonModels != null && cannon < _cannonModels.Length)
            _cannonModels[cannon].DOPunchScale(Vector3.one * 0.25f, WARNING, 8);
        Sfx.Play(Sound.Tick, 0.7f, 0.1f);
    }

    public override void Render()
    {
        base.Render();
        float now = Runner.LocalRenderTime;
        for (int i = _visualBalls.Count - 1; i >= 0; i--)
        {
            var (ball, cannon, tick) = _visualBalls[i];
            if (ball == null) { _visualBalls.RemoveAt(i); continue; }
            var pos = BallPosition(cannon, tick, now);
            ball.position = pos;
            ball.gameObject.SetActive(now - tick * Runner.DeltaTime >= WARNING);
            if ((pos - CannonPosition(cannon)).magnitude > _arenaRadius * 2.2f) { Destroy(ball.gameObject); _visualBalls.RemoveAt(i); }
        }
    }
}
