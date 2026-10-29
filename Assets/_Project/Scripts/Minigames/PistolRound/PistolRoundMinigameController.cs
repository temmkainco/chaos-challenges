using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pistol Round: contestants stand on pedestals in a circle. On your turn you hold the revolver: aim at a rival
/// and press E. The revolver has six chambers and one live round, so every pull is a gamble: a blank just
/// clicks, the live round knocks the target off their pedestal and out. After a live shot the revolver is
/// reloaded. Out of time? The show fires at a random rival for you. Last contestant standing wins.
/// </summary>
public class PistolRoundMinigameController : TurnBasedMinigameController
{
    public const int CHAMBERS = 6;

    [Networked] public int Chamber { get; private set; }

    [SerializeField] private float _aimRange = 40f;
    [SerializeField] private float _aimRadius = 0.6f;
    [SerializeField] private float _launchBack = 11f;
    [SerializeField] private float _launchUp = 7f;
    [SerializeField] private float _benchDelay = 2.5f;
    [SerializeField] private Revolver _revolver;

    private int _liveChamber;
    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private readonly List<(PlayerRef player, TickTimer timer)> _pendingBench = new();

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        int left = CHAMBERS - Chamber;
        return $"Chamber {Chamber + 1}/{CHAMBERS} - 1 live round in the last {left}";
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null) entry.Value.MovementLocked = true;

        Reload();
    }

    private void Reload()
    {
        Chamber = 0;
        _liveChamber = Random.Range(0, CHAMBERS);
    }

    public override bool HandlePrimaryAction(Player player)
    {
        if (!IsGameActive)
            return false;

        if (player.Object.InputAuthority == CurrentTurn)
        {
            var target = FindTarget(player);
            if (target != PlayerRef.None)
                Fire(target);
        }
        // Nothing else to interact with in this round.
        return true;
    }

    protected override void OnTurnTimedOut()
    {
        var targets = _alivePlayers.Where(p => p != CurrentTurn).ToList();
        if (targets.Count > 0)
            Fire(targets[Random.Range(0, targets.Count)]);
        else
            AdvanceTurn();
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

    private void Fire(PlayerRef target)
    {
        var shooter = CurrentTurn;
        bool live = Chamber == _liveChamber;
        Chamber++;
        RPC_Shot(shooter, target, live);
        Debug.Log($"[Pistol] {shooter} fires at {target}: {(live ? "BANG" : "click")}");

        if (!live)
        {
            AdvanceTurn();
            return;
        }

        KnockOff(shooter, target);
        EliminatePlayer(target);
        if (!IsGameActive)
            return;

        Reload();
        AdvanceTurn();
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
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Shot(PlayerRef shooter, PlayerRef target, NetworkBool live)
    {
        if (_revolver != null)
            _revolver.PlayShot(target, live);
    }
}
