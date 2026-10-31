using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Base for last-one-standing minigames on a built arena. During the intro the host places everyone on the arena
/// (<see cref="ArenaSpawn"/>); a contestant is out when they drop below <see cref="OutHeight"/>, touch the kill zone,
/// are eliminated by the game, or leave. Eliminated contestants come back onto the arena as ghosts
/// (<see cref="MinigameController.ReleaseAsGhost"/>): they run around with everyone but can't touch the game, and a
/// ghost that falls off is put back.
/// Placement: survivors first (tied when time runs out), then the eliminated in reverse order.
/// </summary>
public abstract class EliminationMinigameController : MinigameController
{
    [SerializeField, Tooltip("Where knocked-out contestants wait")] protected Transform _bench;

    protected readonly List<PlayerRef> _alive = new();
    private readonly List<PlayerRef> _out = new();
    private readonly List<(PlayerRef player, TickTimer timer)> _toBench = new();

    public bool IsAlive(PlayerRef player) => HasStateAuthority ? _alive.Contains(player) : !IsPlayerLocked(player);


    /// <summary>Contestants still in, replicated for the status line (<see cref="_alive"/> lives on the host only).</summary>
    [Networked] protected int AliveCount { get; private set; }

    public override void Spawned()
    {
        base.Spawned();

    }

    /// <summary>Contestants below this height are out.</summary>
    protected abstract float OutHeight { get; }
    /// <summary>Contestants standing (grounded) above this height are on the arena's rim, which counts as out.</summary>
    protected virtual float RimHeight => float.MaxValue;

    /// <summary>Start position for <paramref name="slot"/> of <paramref name="count"/> contestants.</summary>
    protected abstract Vector3 ArenaSpawn(int slot, int count);

    protected override void OnIntro()
    {
        var players = ConnectedPlayers().OrderBy(p => p.PlayerId).ToList();
        for (int i = 0; i < players.Count; i++)
            if (_playerSpawner.Players.TryGet(players[i], out var player) && player != null)
                _playerSpawner.Teleport(player, ArenaSpawn(i, players.Count));
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        // Anyone who slipped off during the rules card goes back on rather than being out on the first tick.
        var players = ConnectedPlayers().OrderBy(p => p.PlayerId).ToList();
        for (int i = 0; i < players.Count; i++)
            if (_playerSpawner.Players.TryGet(players[i], out var player) && player != null && player.transform.position.y < OutHeight + 0.5f)
                _playerSpawner.Teleport(player, ArenaSpawn(i, players.Count));
        _alive.Clear();
        _alive.AddRange(ConnectedPlayers());
        _out.Clear();
        AliveCount = _alive.Count;
    }

    /// <summary>Host only.</summary>
    protected void Eliminate(PlayerRef player)
    {
        if (!HasStateAuthority || !IsGameActive || !_alive.Remove(player))
            return;
        _out.Add(player);
        AliveCount = _alive.Count;
        SetPlayerInput(player, false);
        _toBench.Add((player, TickTimer.CreateFromSeconds(Runner, 2f)));
        Debug.Log($"[Elimination] {player} out, {_alive.Count} left");
        if (_playerSpawner.Players.TryGet(player, out var p) && p != null)
            RPC_Out(p.transform.position);
        OnEliminated(player);
        if (_alive.Count <= 1)
            FinishRound(Standings());
    }

    protected virtual void OnEliminated(PlayerRef player) { }

    public override bool HandlePlayerFell(Player player)
    {
        if (!IsGameActive) return false;
        if (!HasStateAuthority) return true;
        var who = player.Object.InputAuthority;
        if (_alive.Contains(who)) Eliminate(who);
        else if (_out.Contains(who)) SendToLounge(who);   // a ghost fell off: back onto the arena
        return true;
    }

    /// <summary>Host only. Puts a knocked-out contestant on the lounge with controls back.</summary>
    private void SendToLounge(PlayerRef who)   // (now: back onto the arena as a ghost)
    {
        if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) return;
        ReleaseAsGhost(player, ArenaSpawn(Random.Range(0, 4), 4) + Vector3.up * 0.5f);
    }

    public override void HandlePlayerLeft(PlayerRef player)
    {
        if (IsGameActive) Eliminate(player);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority) return;

        for (int i = _toBench.Count - 1; i >= 0; i--)
        {
            if (!_toBench[i].timer.Expired(Runner)) continue;
            var who = _toBench[i].player;
            _toBench.RemoveAt(i);
            SendToLounge(who);
        }

        if (!IsGameActive) return;
        float outHeight = OutHeight, rim = RimHeight;
        // Ghosts that drop off the arena are put back on it.
        if (Runner.Tick % 10 == 0)
            foreach (var who in _out)
                if (!_toBench.Exists(b => b.player == who) && _playerSpawner.Players.TryGet(who, out var ghost) && ghost != null && ghost.IsSpectator
                    && ghost.transform.position.y < outHeight)
                    SendToLounge(who);
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (!_playerSpawner.Players.TryGet(_alive[i], out var player) || player == null) continue;
            float y = player.transform.position.y;
            bool onRim = y > rim && player.TryGetComponent<PlayerMovement>(out var movement) && movement.IsGrounded
                         && !ArenaProps.IsStandingOnProp(player.transform.position);
            if (y < outHeight || onRim)
                Eliminate(_alive[i]);
        }
    }

    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings() => Standings();

    private IReadOnlyList<IReadOnlyList<PlayerRef>> Standings()
    {
        var standings = new List<IReadOnlyList<PlayerRef>>();
        if (_alive.Count > 0) standings.Add(_alive.ToList());
        for (int i = _out.Count - 1; i >= 0; i--) standings.Add(new[] { _out[i] });
        return standings;
    }

    protected bool IsPlayerLocked(PlayerRef player) =>
        _playerSpawner.Players.TryGet(player, out var p) && p != null && p.ControlsLocked;

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Out(Vector3 position)
    {
        Sfx.Play(Sound.Nope, 0.9f, 0.1f);
        WorldPopup.Show(position + Vector3.up * 2.6f, "OUT!", new Color(1f, 0.37f, 0.36f), 9f);
    }
}
