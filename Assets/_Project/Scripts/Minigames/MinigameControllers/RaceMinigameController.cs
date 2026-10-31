using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Base for races: the host follows everyone's progress along the course (<see cref="Progress"/>), finishers place
/// in order (<see cref="Finish"/>), and at the bell the rest are ranked by how far they got. A fall puts you back at
/// the last checkpoint you reached after a short wait.
/// </summary>
public abstract class RaceMinigameController : MinigameController
{
    [SerializeField] protected Transform _centre;

    [Networked] protected int FinishedCount { get; private set; }
    /// <summary>Per player slot: finishing place (0 = still racing) and the furthest progress, for the scoreboard.</summary>
    [Networked, Capacity(32)] private NetworkArray<byte> Place { get; }
    [Networked, Capacity(32)] private NetworkArray<short> BoardProgress { get; }

    protected readonly List<PlayerRef> _finishOrder = new();
    protected readonly Dictionary<PlayerRef, float> _best = new();
    private readonly List<(PlayerRef player, TickTimer timer)> _respawns = new();

    /// <summary>How far along the course <paramref name="position"/> is (bigger is further; the host calls it every tick).</summary>
    protected abstract float Progress(PlayerRef player, Vector3 position);
    /// <summary>Where a contestant who fell restarts, given the furthest progress they reached.</summary>
    protected abstract Vector3 RespawnPoint(PlayerRef player, float bestProgress, out float yaw);
    protected abstract Vector3 StartPosition(int slot, int count, out float yaw);
    /// <summary>Whether <paramref name="progress"/> crosses the finish.</summary>
    protected abstract bool IsFinished(float progress);
    /// <summary>Below this height a contestant has fallen off the course.</summary>
    protected virtual float FallHeight => float.MinValue;

    protected override void OnIntro()
    {
        var players = ConnectedPlayers().OrderBy(p => p.PlayerId).ToList();
        for (int i = 0; i < players.Count; i++)
            if (_playerSpawner.Players.TryGet(players[i], out var player) && player != null)
            {
                var spot = StartPosition(i, players.Count, out float yaw);
                _playerSpawner.Teleport(player, spot, Quaternion.Euler(0f, yaw, 0f));
                if (player.TryGetComponent<PlayerMovement>(out var movement)) movement.ResetVehicle(yaw);
            }
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        _finishOrder.Clear();
        _best.Clear();
        _respawns.Clear();
        FinishedCount = 0;
        for (int i = 0; i < 32; i++) { Place.Set(i, 0); BoardProgress.Set(i, 0); }
    }

    public override string GetStatusText() => null;

    /// <summary>How the scoreboard shows a racer's progress (e.g. "Lap 2", "Row 4").</summary>
    protected virtual string ProgressLabel(float progress) => Mathf.FloorToInt(progress).ToString();

    public override string ScoreboardValue(PlayerRef player)
    {
        if (!IsGameActive && Phase != MinigamePhase.Finished) return null;
        int slot = player.PlayerId & 31;
        return Place[slot] > 0 ? $"#{Place[slot]}" : ProgressLabel(BoardProgress[slot]);
    }

    public override float ScoreboardRank(PlayerRef player)
    {
        int slot = player.PlayerId & 31;
        return Place[slot] > 0 ? 100000 - Place[slot] : BoardProgress[slot];
    }

    public override bool HandlePlayerFell(Player player)
    {
        if (!IsGameActive) return false;
        if (!HasStateAuthority) return true;
        var who = player.Object.InputAuthority;
        if (_respawns.Exists(r => r.player == who) || _finishOrder.Contains(who)) return true;
        player.ControlsLocked = true;
        _respawns.Add((who, TickTimer.CreateFromSeconds(Runner, 0.8f)));
        return true;
    }

    /// <summary>Host only. Sends a contestant back to their checkpoint now (e.g. hit by an obstacle).</summary>
    protected void SendToCheckpoint(PlayerRef who)
    {
        if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) return;
        _best.TryGetValue(who, out float best);
        var spot = RespawnPoint(who, best, out float yaw);
        _playerSpawner.Teleport(player, spot + Vector3.up * 0.5f, Quaternion.Euler(0f, yaw, 0f));
        if (player.TryGetComponent<PlayerMovement>(out var movement)) movement.ResetVehicle(yaw);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;

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
            SendToCheckpoint(who);
            if (_playerSpawner.Players.TryGet(who, out var p) && p != null) p.ControlsLocked = false;
        }

        foreach (var entry in _playerSpawner.Players)
        {
            var who = entry.Key;
            var player = entry.Value;
            if (player == null || _finishOrder.Contains(who) || _respawns.Exists(r => r.player == who)) continue;
            float progress = Progress(who, player.transform.position);
            if (!_best.TryGetValue(who, out float best) || progress > best) _best[who] = progress;
            BoardProgress.Set(who.PlayerId & 31, (short)Mathf.Clamp(Mathf.FloorToInt(_best[who]), short.MinValue, short.MaxValue));
            if (IsFinished(progress)) Finish(who);
        }
    }

    /// <summary>Host only.</summary>
    protected void Finish(PlayerRef player)
    {
        if (!IsGameActive || _finishOrder.Contains(player)) return;
        _finishOrder.Add(player);
        FinishedCount = _finishOrder.Count;
        Place.Set(player.PlayerId & 31, (byte)_finishOrder.Count);
        if (_playerSpawner.Players.TryGet(player, out var p) && p != null)
        {
            p.ControlsLocked = true;
            RPC_Finished(p.transform.position, _finishOrder.Count, player);
        }
        if (!ConnectedPlayers().Any(x => !_finishOrder.Contains(x)))
            FinishRound(_finishOrder.ToList());
    }

    public override void HandlePlayerLeft(PlayerRef player)
    {
        if (IsGameActive && ConnectedPlayers().All(p => _finishOrder.Contains(p)))
            FinishRound(_finishOrder.ToList());
    }

    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings()
    {
        var standings = _finishOrder.Select(p => (IReadOnlyList<PlayerRef>)new[] { p }).ToList();
        standings.AddRange(ConnectedPlayers().Where(p => !_finishOrder.Contains(p))
            .OrderByDescending(p => _best.TryGetValue(p, out float b) ? b : float.MinValue)
            .Select(p => (IReadOnlyList<PlayerRef>)new[] { p }));
        return standings;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Finished(Vector3 position, int place, PlayerRef player)
    {
        string text = place switch { 1 => "1ST", 2 => "2ND", 3 => "3RD", _ => place + "TH" };
        WorldPopup.Show(position + Vector3.up * 3f, $"FINISH! {text}", new Color(1f, 0.79f, 0.24f), 10f);
        Sfx.Play(player == Runner.LocalPlayer ? Sound.Fanfare : Sound.Pop, 1f, 0f);
    }
}
