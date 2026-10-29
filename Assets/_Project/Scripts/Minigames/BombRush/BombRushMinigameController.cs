using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Bomb Rush: conveyors keep feeding bombs into the arena. Grab one (E), throw it (E again) at a rival:
/// thrown bombs go off on contact, lit ones on their own after the fuse. Blasts launch everyone nearby;
/// whoever falls off the arena is out. Last contestant standing wins; on time-up the survivors tie.
/// </summary>
public class BombRushMinigameController : MinigameController
{
    [Header("Bombs")]
    [SerializeField] private NetworkPrefabRef _bombPrefab;
    [SerializeField, Tooltip("Seconds between bombs with 2 players; more players get them faster")] private float _spawnInterval = 3f;
    [SerializeField] private int _maxBombsPerPlayer = 2;
    [SerializeField, Tooltip("Last seconds of the round with double bomb rate")] private float _suddenDeathSeconds = 40f;

    [Header("Blast")]
    [SerializeField] private float _blastRadius = 5f;
    [SerializeField] private float _blastLaunch = 16f;
    [SerializeField] private float _blastLift = 9f;
    [SerializeField] private Material _effectMaterial;

    [Header("Arena")]
    [SerializeField] private Transform _bench;

    [Networked] private int AliveCount { get; set; }
    [Networked] private TickTimer SpawnTimer { get; set; }

    private ConveyorBelt[] _belts;
    private readonly List<PlayerRef> _alive = new();
    private readonly List<PlayerRef> _eliminationOrder = new();
    private int _nextBelt;

    public override void Spawned()
    {
        base.Spawned();
        _belts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.InstanceID);
    }

    public ConveyorBelt GetBelt(int index) => index >= 0 && index < _belts.Length ? _belts[index] : null;

    private bool SuddenDeath => IsTimeLimited && IsGameActive && TimeRemaining <= _suddenDeathSeconds;

    public override string GetStatusText()
    {
        string alive = AliveCount == 1 ? "1 contestant left" : $"{AliveCount} contestants left";
        return SuddenDeath ? $"<color=#FF6A50>SUDDEN DEATH!</color> {alive}" : alive;
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        _alive.Clear();
        _alive.AddRange(ConnectedPlayers());
        _eliminationOrder.Clear();
        AliveCount = _alive.Count;
        SpawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive || _belts.Length == 0)
            return;

        if (!SpawnTimer.Expired(Runner))
            return;

        float interval = _spawnInterval * 2f / Mathf.Max(2, _alive.Count) * (SuddenDeath ? 0.5f : 1f);
        SpawnTimer = TickTimer.CreateFromSeconds(Runner, interval);

        if (FindObjectsByType<Bomb>(FindObjectsSortMode.None).Length >= _alive.Count * _maxBombsPerPlayer * (SuddenDeath ? 2 : 1))
            return;

        int belt = _nextBelt++ % _belts.Length;
        Runner.Spawn(_bombPrefab, _belts[belt].Evaluate(0f), Quaternion.identity, null,
            (runner, obj) => obj.GetComponent<Bomb>().Initialize(this, belt));
    }

    /// <summary>Host only. Is any player other than <paramref name="ignore"/> within <paramref name="radius"/>?</summary>
    public bool AnyPlayerWithin(Vector3 position, float radius, PlayerRef ignore)
    {
        foreach (var entry in _playerSpawner.Players)
        {
            if (entry.Key == ignore || entry.Value == null || !_alive.Contains(entry.Key))
                continue;

            var centre = entry.Value.transform.position + Vector3.up;
            if ((centre - position).sqrMagnitude <= radius * radius)
                return true;
        }
        return false;
    }

    /// <summary>Host only. Launches everyone in range; whoever holds the bomb takes the full blast.</summary>
    public void Explode(Vector3 position, PlayerRef holder)
    {
        RPC_Explosion(position);
        if (!IsGameActive)
            return;

        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || !_alive.Contains(entry.Key))
                continue;

            var offset = player.transform.position - position;
            float distance = offset.magnitude;
            bool holding = entry.Key == holder;
            if (!holding && distance > _blastRadius)
                continue;

            float strength = holding ? 1.3f : Mathf.Lerp(1f, 0.35f, distance / _blastRadius);
            var away = new Vector3(offset.x, 0f, offset.z);
            away = away.sqrMagnitude < 0.01f ? Random.insideUnitSphere : away.normalized;
            away.y = 0f;

            if (player.TryGetComponent<PlayerMovement>(out var movement))
                movement.Launch(away.normalized * _blastLaunch * strength + Vector3.up * _blastLift * strength);
        }
    }

    public override bool HandlePlayerFell(Player player)
    {
        if (!IsGameActive)
            return false;

        if (HasStateAuthority)
            Eliminate(player.Object.InputAuthority);
        return true;
    }

    public override void HandlePlayerLeft(PlayerRef player)
    {
        if (IsGameActive)
            Eliminate(player);
    }

    private void Eliminate(PlayerRef player)
    {
        if (!_alive.Remove(player))
            return;

        _eliminationOrder.Add(player);
        AliveCount = _alive.Count;
        SetPlayerInput(player, false);
        if (_bench != null && _playerSpawner.Players.TryGet(player, out var playerObject) && playerObject != null)
            _playerSpawner.Teleport(playerObject, _bench.position + Random.insideUnitSphere.WithY(0f) * 2f);

        Debug.Log($"[BombRush] {player} is out, {_alive.Count} left");

        if (_alive.Count <= 1)
        {
            var standings = _alive.Concat(Enumerable.Reverse(_eliminationOrder)).ToList();
            FinishRound(standings);
        }
    }

    /// <summary>Survivors tie for first; the rest by how long they lasted.</summary>
    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings()
    {
        var standings = new List<IReadOnlyList<PlayerRef>> { _alive.ToList() };
        standings.AddRange(Enumerable.Reverse(_eliminationOrder).Select(p => (IReadOnlyList<PlayerRef>)new[] { p }));
        return standings;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Explosion(Vector3 position)
    {
        Sfx.Play(Sound.Boom);
        if (_effectMaterial != null)
            LowPolyExplosion.Play(position, 3f, _effectMaterial);
    }
}

internal static class VectorExtensions
{
    public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);
}
