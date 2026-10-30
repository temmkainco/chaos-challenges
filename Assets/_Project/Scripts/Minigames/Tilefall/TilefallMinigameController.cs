using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

/// <summary>
/// Tilefall: race across lanes of tiles that crack under your feet (faster when crowded) to the finish.
/// Falling costs a short delay and puts you back on the last checkpoint row you reached. Finishers place in
/// order; when time runs out the rest are ranked by how far they got.
/// </summary>
public class TilefallMinigameController : MinigameController
{
    [Networked] private int PlayersFinished { get; set; }

    [Inject] private TileCourse _course;

    [SerializeField] private float _fallDelay = 1.5f;

    private readonly List<PlayerRef> _finishOrder = new();
    private readonly Dictionary<PlayerRef, float> _bestProgress = new();
    private readonly Dictionary<PlayerRef, TickTimer> _pendingRespawns = new();
    private readonly List<Vector3> _standing = new();
    private readonly Dictionary<PlayerRef, int> _protectedUntil = new();

    /// <summary>Host only: just respawned, so hazards leave them alone for a moment.</summary>
    public bool IsProtected(PlayerRef player) => _protectedUntil.TryGetValue(player, out int until) && Runner.Tick < until;

    public event Action<PlayerRef, int> OnPlayerFinished;

    public override string[] Variants => TileCourse.Layouts;

    public override string TwistTitle => "CROSSWIND";
    public override string TwistDescription => "A gale blows across the course and flips direction every few seconds.";

    /// <summary>Across the lanes, flipping every 4 seconds of the shared tick clock (same on host and predicting clients).</summary>
    private int WindSign => (Runner.Tick / (Runner.TickRate * 4)) % 2 == 0 ? 1 : -1;
    public override Vector3 ChaosWind => TwistActive ? _course.transform.right * (3.4f * WindSign) : Vector3.zero;

    public override string GetStatusText() =>
        IsGameActive && TwistActive ? (WindSign > 0 ? "Wind blowing to the RIGHT" : "Wind blowing to the LEFT") : null;

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        PlayersFinished = 0;
        _finishOrder.Clear();
        _bestProgress.Clear();
        _pendingRespawns.Clear();
    }

    /// <summary>Host only (the finish line is checked on the host).</summary>
    public void PlayerFinished(PlayerRef player)
    {
        if (!HasStateAuthority || !IsGameActive || _finishOrder.Contains(player))
            return;

        _finishOrder.Add(player);
        PlayersFinished = _finishOrder.Count;
        Debug.Log($"Player {player} finished #{PlayersFinished}");
        RPC_OnPlayerFinished(player, PlayersFinished);
        string name = _playerSpawner.Players.TryGet(player, out var p) && p != null && !string.IsNullOrEmpty(p.Nickname) ? p.Nickname : $"Player {player.PlayerId}";
        Announce($"{(name.Length > 14 ? name.Substring(0, 14) : name)} FINISHED #{PlayersFinished}!");

        CheckRaceOver();
    }

    public override void HandlePlayerLeft(PlayerRef player)
    {
        _pendingRespawns.Remove(player);
        CheckRaceOver();
    }

    public override bool HandlePlayerFell(Player player)
    {
        if (!IsGameActive)
            return false;

        if (HasStateAuthority && !_pendingRespawns.ContainsKey(player.Object.InputAuthority))
        {
            player.ControlsLocked = true;
            _pendingRespawns[player.Object.InputAuthority] = TickTimer.CreateFromSeconds(Runner, _fallDelay);
        }
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority || !IsGameActive)
            return;

        _standing.Clear();
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || _pendingRespawns.ContainsKey(entry.Key))
                continue;

            float progress = _course.Progress(player.transform.position);
            if (!_bestProgress.TryGetValue(entry.Key, out float best) || progress > best)
                _bestProgress[entry.Key] = progress;

            if (player.TryGetComponent<NetworkCharacterController>(out var controller) && controller.IsGrounded)
                _standing.Add(player.transform.position);
        }
        _course.Simulate(_standing);

        RespawnFallenPlayers();
    }

    private void RespawnFallenPlayers()
    {
        foreach (var player in _pendingRespawns.Where(p => p.Value.Expired(Runner)).Select(p => p.Key).ToList())
        {
            _pendingRespawns.Remove(player);
            if (!_playerSpawner.Players.TryGet(player, out var playerObject) || playerObject == null)
                continue;

            _bestProgress.TryGetValue(player, out float best);
            int checkpoint = _course.CheckpointBehind(best);
            int lane = _course.LaneAt(playerObject.transform.position);
            // Back on the last checkpoint row, or the start platform (never a generic spawn point over a gap).
            var position = (checkpoint >= 0 ? _course.TilePosition(lane, checkpoint) : _course.StartPosition(lane)) + Vector3.up;

            _playerSpawner.Teleport(playerObject, position);
            playerObject.ControlsLocked = false;
            _protectedUntil[player] = Runner.Tick + Mathf.CeilToInt(2f * Runner.TickRate);
        }
    }

    private void CheckRaceOver()
    {
        if (!IsGameActive)
            return;

        if (!ConnectedPlayers().Any(p => !_finishOrder.Contains(p)))
            FinishRound(_finishOrder);
    }

    /// <summary>Finishers keep their order; the rest are ranked by the furthest row they reached (same row ties).</summary>
    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings()
    {
        var standings = _finishOrder.Select(p => (IReadOnlyList<PlayerRef>)new[] { p }).ToList();
        var stillRacing = ConnectedPlayers()
            .Where(p => !_finishOrder.Contains(p))
            .GroupBy(p => _bestProgress.TryGetValue(p, out float best) ? Mathf.FloorToInt(best) : int.MinValue)
            .OrderByDescending(g => g.Key)
            .Select(g => (IReadOnlyList<PlayerRef>)g.ToList());
        standings.AddRange(stillRacing);
        return standings;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnPlayerFinished(PlayerRef player, int position)
    {
        OnPlayerFinished?.Invoke(player, position);
        // Finish feedback: a popup over the finisher and a cheer; your own finish also gets the big banner.
        string place = position switch { 1 => "1ST", 2 => "2ND", 3 => "3RD", _ => position + "TH" };
        if (_playerSpawner.Players.TryGet(player, out var finisher) && finisher != null)
            WorldPopup.Show(finisher.transform.position + Vector3.up * 3f, $"FINISH! {place}", new Color(1f, 0.79f, 0.24f), 10f);
        Sfx.Play(player == Runner.LocalPlayer ? Sound.Fanfare : Sound.Pop, 1f, 0f);
    }
}
