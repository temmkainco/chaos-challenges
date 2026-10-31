using Fusion;
using System.Collections.Generic;
using UnityEngine;

public enum DuelStage : byte
{
    Steady,
    Draw,
    Settle,
}

/// <summary>
/// Pistol Round as a quick-draw duel: every contestant stands on a pedestal with a revolver. "Steady..." lasts a
/// random few seconds, then the show calls "DRAW!" and the first accurate shot knocks its target off their pedestal
/// and out. Shooting before the call, or missing, jams your revolver until the next draw. If nobody hits in time the
/// draw is replayed. Reflexes and aim decide it, not luck. Last contestant standing wins.
/// </summary>
public class PistolRoundMinigameController : TurnBasedMinigameController
{
    /// <summary>The street has its own buildings and fences.</summary>
    protected override bool UsesBoundary => false;

    [Networked] public DuelStage Stage { get; private set; }
    [Networked] private TickTimer StageTimer { get; set; }
    /// <summary>Bit per <see cref="PlayerRef.PlayerId"/>: jammed until the next draw.</summary>
    [Networked] private int Jammed { get; set; }

    [SerializeField] private float _aimRange = 40f;
    [SerializeField] private float _aimRadius = 0.6f;
    [SerializeField] private float _launchBack = 11f;
    [SerializeField] private float _launchUp = 7f;
    [SerializeField] private float _benchDelay = 2.5f;
    [SerializeField] private Vector2 _steadySeconds = new(2f, 5f);
    [SerializeField] private float _drawWindow = 3f;
    [SerializeField] private float _settleSeconds = 2f;
    [SerializeField] private Revolver _revolver;

    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private readonly List<(PlayerRef player, TickTimer timer)> _pendingBench = new();

    public bool IsJammed(PlayerRef player) => (Jammed & (1 << player.PlayerId)) != 0;

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        if (IsJammed(Runner.LocalPlayer)) return "<color=#E03131>Jammed!</color> Wait for the next draw";
        return Stage switch
        {
            DuelStage.Steady => "Steady... shoot only on DRAW!",
            DuelStage.Draw => "<color=#E8590C>DRAW!</color>",
            _ => "",
        };
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null) entry.Value.MovementLocked = true;

        BeginSteady();
    }

    // No turns: everyone shoots at once.
    protected override void PickFirstTurn() { }
    protected override void OnTurnTimedOut() { }

    private void BeginSteady()
    {
        Stage = DuelStage.Steady;
        Jammed = 0;
        StageTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(_steadySeconds.x, _steadySeconds.y));
        Announce("STEADY...");
    }

    public override bool HandlePrimaryAction(Player player)
    {
        if (!IsGameActive)
            return false;

        var shooter = player.Object.InputAuthority;
        if (!_alivePlayers.Contains(shooter) || IsJammed(shooter) || Stage == DuelStage.Settle)
            return true;

        if (Stage == DuelStage.Steady)
        {
            // Jumped the gun.
            Jam(shooter);
            RPC_Shot(shooter, PlayerRef.None, false);
            return true;
        }

        var target = FindTarget(player);
        if (target == PlayerRef.None)
        {
            // Missed: one bullet per draw.
            Jam(shooter);
            RPC_Shot(shooter, PlayerRef.None, false);
            return true;
        }

        Fire(shooter, target);
        return true;
    }

    private void Jam(PlayerRef player)
    {
        Jammed |= 1 << player.PlayerId;
        Debug.Log($"[Pistol] {player} jammed ({Stage})");
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority) return;

        for (int i = _pendingBench.Count - 1; i >= 0; i--)
        {
            var (player, timer) = _pendingBench[i];
            if (!timer.Expired(Runner)) continue;

            _pendingBench.RemoveAt(i);
            if (_playerSpawner.Players.TryGet(player, out var playerObject) && playerObject != null)
                _playerSpawner.RequestSpawn(playerObject);
        }

        if (!IsGameActive || !StageTimer.Expired(Runner))
            return;

        switch (Stage)
        {
            case DuelStage.Steady:
                Stage = DuelStage.Draw;
                StageTimer = TickTimer.CreateFromSeconds(Runner, _drawWindow);
                Announce("DRAW!");
                break;
            case DuelStage.Draw:
                // Nobody hit anything: replay the draw.
                Stage = DuelStage.Settle;
                StageTimer = TickTimer.CreateFromSeconds(Runner, _settleSeconds);
                Announce("TOO SLOW!");
                break;
            default:
                BeginSteady();
                break;
        }
    }

    /// <summary>The alive rival the player is aiming at (sphere cast along their look direction), or None.</summary>
    public PlayerRef FindTarget(Player shooter)
    {
        var look = shooter.Interaction != null ? shooter.Interaction.LookDirection : shooter.transform.forward;
        if (look.sqrMagnitude < 0.01f) look = shooter.transform.forward;

        var origin = shooter.transform.position + Vector3.up * 1.6f;
        int count = Runner.GetPhysicsScene().SphereCast(origin, _aimRadius, look.normalized, _hits, _aimRange, ~0, QueryTriggerInteraction.Ignore);

        PlayerRef best = PlayerRef.None;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var player = _hits[i].collider.GetComponentInParent<Player>();
            if (player == null || player == shooter || player.Object == null) continue;

            // Clients do not have the host's alive list; eliminated players are the ones with locked controls.
            var target = player.Object.InputAuthority;
            bool alive = HasStateAuthority ? _alivePlayers.Contains(target) : !player.ControlsLocked;
            if (!alive || _hits[i].distance >= bestDistance) continue;

            best = target;
            bestDistance = _hits[i].distance;
        }
        return best;
    }

    private void Fire(PlayerRef shooter, PlayerRef target)
    {
        RPC_Shot(shooter, target, true);
        Debug.Log($"[Pistol] {shooter} hits {target}");

        KnockOff(shooter, target);
        EliminatePlayer(target);
        if (!IsGameActive)
            return;

        Stage = DuelStage.Settle;
        StageTimer = TickTimer.CreateFromSeconds(Runner, _settleSeconds);
    }

    private void KnockOff(PlayerRef shooter, PlayerRef target)
    {
        if (!_playerSpawner.Players.TryGet(target, out var targetObject) || targetObject == null)
            return;

        var away = Vector3.forward;
        if (_playerSpawner.Players.TryGet(shooter, out var shooterObject) && shooterObject != null)
            away = targetObject.transform.position - shooterObject.transform.position;
        away.y = 0f;
        away = away.sqrMagnitude < 0.01f ? Vector3.forward : away.normalized;

        targetObject.MovementLocked = false;
        if (targetObject.TryGetComponent<PlayerMovement>(out var movement))
            movement.Launch(away * _launchBack + Vector3.up * _launchUp);

        _pendingBench.Add((target, TickTimer.CreateFromSeconds(Runner, _benchDelay)));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Shot(PlayerRef shooter, PlayerRef target, NetworkBool hit)
    {
        if (_revolver != null)
            _revolver.PlayShot(shooter, target, hit);
    }
}
