using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Base for points minigames: everyone plays the whole round, the host adds points (<see cref="AddScore"/>), the
/// most points at the bell wins (equal scores tie). Contestants are placed with <see cref="ArenaSpawn"/> during the
/// intro, and anyone who falls off is put back on the arena after a short wait.
/// </summary>
public abstract class ScoreMinigameController : MinigameController
{
    private const int SLOTS = 32;

    [SerializeField] protected Transform _centre;

    /// <summary>Points per player (indexed by <see cref="Slot"/>), replicated for the HUD.</summary>
    [Networked, Capacity(SLOTS)] private NetworkArray<int> Scores { get; }

    private readonly List<(PlayerRef player, TickTimer timer)> _respawns = new();

    private static int Slot(PlayerRef player) => player.PlayerId & (SLOTS - 1);
    public int ScoreOf(PlayerRef player) => Scores[Slot(player)];

    /// <summary>"pts", "tiles", "rings"... for the status line.</summary>
    protected virtual string ScoreUnit => "pts";

    protected abstract Vector3 ArenaSpawn(int slot, int count);
    /// <summary>Below this height a contestant has fallen off the arena.</summary>
    protected virtual float FallHeight => float.MinValue;

    /// <summary>Host only.</summary>
    protected void AddScore(PlayerRef player, int points)
    {
        if (HasStateAuthority && IsGameActive) Scores.Set(Slot(player), Mathf.Max(0, Scores[Slot(player)] + points));
    }

    /// <summary>Host only (e.g. Paint Party counts tiles: the score is a total, not a running sum).</summary>
    protected void SetScore(PlayerRef player, int points)
    {
        if (HasStateAuthority) Scores.Set(Slot(player), points);
    }

    protected override void OnIntro()
    {
        var players = ConnectedPlayers().OrderBy(p => p.PlayerId).ToList();
        for (int i = 0; i < players.Count; i++)
            if (_playerSpawner.Players.TryGet(players[i], out var player) && player != null)
                _playerSpawner.Teleport(player, ArenaSpawn(i, players.Count), Quaternion.LookRotation((ArenaCentre - ArenaSpawn(i, players.Count)).WithY(0f).normalized + Vector3.forward * 0.001f));
    }

    protected virtual Vector3 ArenaCentre => _centre != null ? _centre.position : transform.position;

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        for (int i = 0; i < SLOTS; i++) Scores.Set(i, 0);
        _respawns.Clear();
    }

    // Scores live on the side scoreboard; the status line stays free for the game's own short notes.
    public override string GetStatusText() => null;
    public override string ScoreboardValue(PlayerRef player) => IsGameActive || Phase == MinigamePhase.Finished ? ScoreOf(player).ToString() : null;
    public override float ScoreboardRank(PlayerRef player) => ScoreOf(player);

    public override bool HandlePlayerFell(Player player)
    {
        if (!IsGameActive) return false;
        if (!HasStateAuthority) return true;
        var who = player.Object.InputAuthority;
        if (_respawns.Exists(r => r.player == who)) return true;
        player.ControlsLocked = true;
        _respawns.Add((who, TickTimer.CreateFromSeconds(Runner, 1f)));
        OnFell(who);
        return true;
    }

    /// <summary>Host only: a contestant fell off (before they are put back).</summary>
    protected virtual void OnFell(PlayerRef player) { }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority) return;
        // Raised arenas: below this you fell (the scene's own fall guard is under the studio floor).
        if (IsGameActive && Runner.Tick % 5 == 0)
            foreach (var entry in _playerSpawner.Players)
                if (entry.Value != null && entry.Value.transform.position.y < FallHeight)
                    HandlePlayerFell(entry.Value);
        for (int i = _respawns.Count - 1; i >= 0; i--)
        {
            if (!_respawns[i].timer.Expired(Runner)) continue;
            var who = _respawns[i].player;
            _respawns.RemoveAt(i);
            if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) continue;
            var spot = ArenaSpawn(Random.Range(0, 6), 6) + Vector3.up * 0.5f;
            var facing = (ArenaCentre - spot).WithY(0f);
            float yaw = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing).eulerAngles.y : player.transform.eulerAngles.y;
            _playerSpawner.Teleport(player, spot, Quaternion.Euler(0f, yaw, 0f));
            if (player.TryGetComponent<PlayerMovement>(out var movement)) movement.ResetVehicle(yaw);
            player.ControlsLocked = !IsGameActive;
        }
    }

    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings() =>
        ConnectedPlayers().GroupBy(ScoreOf).OrderByDescending(g => g.Key).Select(g => (IReadOnlyList<PlayerRef>)g.ToList()).ToList();
}
