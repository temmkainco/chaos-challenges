using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Bomb Rush, island wars: everyone stands on their own grass island in a ring above the scrapyard, and every few
/// seconds a bomb drops onto each island. Grab it (E), throw it (E again) at a rival's island: thrown bombs go off on
/// contact, lit ones on their own after the fuse. A bomb going off in your hands or right next to you knocks you out;
/// farther away it blasts you, and whoever is blown off their island is out. Knocked-out contestants watch from the
/// scrapyard below. Last contestant standing wins; on time-up the survivors tie. Layouts: Islands, Close Islands
/// (close enough to jump across), Crate Islands (crates for cover). Twist: BOMB BONANZA, bombs rain on the islands.
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
    private readonly List<(PlayerRef player, TickTimer timer)> _toLounge = new();

    /// <summary>A blast this close (or in your hands) knocks you out; farther away it only launches you.</summary>
    public const float KILL_RADIUS = 3f;
    private Vector3 _arenaCentre;

    public override void Spawned()
    {
        base.Spawned();
        _belts = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.InstanceID);
        var centre = Vector3.zero;
        foreach (var belt in _belts) centre += belt.Evaluate(1f);
        centre = _belts.Length > 0 ? centre / _belts.Length : transform.position;
        var arenaCentre = GameObject.Find("ArenaCentre");
        _arenaCentre = arenaCentre != null ? arenaCentre.transform.position : centre;
        // The belt ends sit above the stage: drop the centre onto the stage floor (out-of-arena checks and props use it).
        if (Physics.Raycast(_arenaCentre + Vector3.up * 0.5f, Vector3.down, out var floorHit, 6f, ~0, QueryTriggerInteraction.Ignore))
            _arenaCentre.y = floorHit.point.y;
        BuildIslands();
    }

    public override string[] Variants => new[] { "Islands", "Close Islands", "Crate Islands" };

    private const int ISLANDS = 6;
    private const float ISLAND_SIZE = 6f, ISLAND_HEIGHT = 7f;
    private float RingRadius => Variant == 1 ? 9.6f : 12.5f;
    private Vector3 Island(int i)
    {
        float angle = (i + 0.5f) / ISLANDS * Mathf.PI * 2f;
        return _arenaCentre + Vector3.up * ISLAND_HEIGHT + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * RingRadius;
    }
    private float IslandY => _arenaCentre.y + ISLAND_HEIGHT;

    /// <summary>Host only: which island each contestant got, and when their next bomb drops.</summary>
    private readonly Dictionary<PlayerRef, int> _islandOf = new();
    private readonly Dictionary<PlayerRef, TickTimer> _supply = new();

    private void BuildIslands()
    {
        for (int i = 0; i < ISLANDS; i++)
        {
            var top = Island(i);
            float yaw = (i + 0.5f) / ISLANDS * -360f;
            ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cube, top + Vector3.down * 1.2f, new Vector3(ISLAND_SIZE, 1.2f, ISLAND_SIZE), ArenaProps.Green, yaw), "Grass");
            if (Variant == 2)
            {
                var outward = (top - _arenaCentre).WithY(0f).normalized;
                ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cube, top - outward * 1.6f + Vector3.Cross(Vector3.up, outward) * 1.3f,
                    new Vector3(1.3f, 1.3f, 1.3f), ArenaProps.Yellow, yaw), "Crate");
            }
        }
        ArenaProps.Done();
    }

    /// <summary>Host. Everyone onto their own island, spread round the ring, facing the middle.</summary>
    protected override void OnIntro()
    {
        base.OnIntro();
        var players = ConnectedPlayers().OrderBy(p => p.PlayerId).ToList();
        _islandOf.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            int island = i * ISLANDS / Mathf.Max(1, players.Count);
            _islandOf[players[i]] = island;
            if (_playerSpawner.Players.TryGet(players[i], out var player) && player != null)
                _playerSpawner.Teleport(player, Island(island) + Vector3.up * 0.6f, Quaternion.LookRotation((_arenaCentre - Island(island)).WithY(0f)));
        }
    }

    /// <summary>Host. Drops an unlit bomb on every island that has none lying on it.</summary>
    private void Supply()
    {
        foreach (var who in _alive)
        {
            if (!_islandOf.TryGetValue(who, out int island)) continue;
            if (_supply.TryGetValue(who, out var timer) && !timer.ExpiredOrNotRunning(Runner)) continue;
            var top = Island(island);
            bool stocked = false;
            foreach (var bomb in Bomb.Active)
                if (bomb != null && !bomb.Lit && (bomb.transform.position - top).WithY(0f).magnitude < ISLAND_SIZE * 0.6f && Mathf.Abs(bomb.transform.position.y - top.y) < 3f)
                    stocked = true;
            _supply[who] = TickTimer.CreateFromSeconds(Runner, SuddenDeath ? 2f : 3.2f);
            if (stocked) continue;
            Runner.Spawn(_bombPrefab, top + Vector3.up * 1.6f + Random.insideUnitSphere.WithY(0f), Quaternion.identity, null,
                (runner, obj) => obj.GetComponent<Bomb>().Initialize(this, -1));
        }
    }


    public ConveyorBelt GetBelt(int index) => index >= 0 && index < _belts.Length ? _belts[index] : null;

    private bool SuddenDeath => IsTimeLimited && IsGameActive && TimeRemaining <= _suddenDeathSeconds;

    public override string TwistTitle => "BOMB BONANZA";
    public override string TwistDescription => "Lit bombs rain down on the islands.";

    public override string GetStatusText()
    {
        string alive = AliveCount == 1 ? "1 contestant left" : $"{AliveCount} contestants left";
        return SuddenDeath ? $"<color=#E03131>SUDDEN DEATH!</color> {alive}" : alive;
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
        _supply.Clear();
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        // Blown off your island (below it) is out; ghosts watch from the scrapyard and are put back if they fall out of it.
        if (HasStateAuthority && IsGameActive && Runner.Tick % 10 == 0)
        {
            float off = IslandY - 3f, floor = _arenaCentre.y - 1f;
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (!_playerSpawner.Players.TryGet(_alive[i], out var p) || p == null) continue;
                if (p.transform.position.y < off)
                    Eliminate(_alive[i]);
            }
            foreach (var who in _eliminationOrder)
                if (!_toLounge.Exists(t => t.player == who) && _playerSpawner.Players.TryGet(who, out var ghost) && ghost != null
                    && ghost.IsSpectator && ghost.transform.position.y < floor)
                    SendToLounge(who);
        }
        if (HasStateAuthority)
            for (int i = _toLounge.Count - 1; i >= 0; i--)
                if (_toLounge[i].timer.Expired(Runner))
                {
                    var who = _toLounge[i].player;
                    _toLounge.RemoveAt(i);
                    SendToLounge(who);
                }
        if (!HasStateAuthority || !IsGameActive)
            return;

        Supply();

        // Bomb Bonanza: now and then a lit bomb falls onto a random island (it can't be caught).
        if (!TwistActive || !SpawnTimer.Expired(Runner))
            return;
        SpawnTimer = TickTimer.CreateFromSeconds(Runner, 1.6f);
        var target = Island(Random.Range(0, ISLANDS)) + Random.insideUnitSphere.WithY(0f) * 2f;
        Runner.Spawn(_bombPrefab, target + Vector3.up * 12f, Quaternion.identity, null,
            (runner, obj) => { var bomb = obj.GetComponent<Bomb>(); bomb.Initialize(this, -1); bomb.FromSky = true; });
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
            if (holding || distance <= KILL_RADIUS)
            {
                RPC_Out(player.transform.position);
                Eliminate(entry.Key);
            }
        }
    }

    public override bool HandlePlayerFell(Player player)
    {
        if (!IsGameActive)
            return false;

        if (HasStateAuthority)
        {
            var who = player.Object.InputAuthority;
            if (_alive.Contains(who)) Eliminate(who);
            else if (_eliminationOrder.Contains(who)) SendToLounge(who);   // a ghost fell off: back onto the arena
        }
        return true;
    }

    /// <summary>Host only. Knocked-out contestants watch from the lounge, free to move.</summary>
    private void SendToLounge(PlayerRef who)
    {
        if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) return;
        var flat = Random.insideUnitCircle * 8f;
        ReleaseAsGhost(player, _arenaCentre + new Vector3(flat.x, 1f, flat.y));
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
        // Let the blast play out, then off to the lounge.
        _toLounge.Add((player, TickTimer.CreateFromSeconds(Runner, 2f)));
        if (_playerSpawner.Players.TryGet(player, out var held) && held != null && held.Interaction != null)
            held.Interaction.ForceDrop();

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
    private void RPC_Out(Vector3 position)
    {
        WorldPopup.Show(position + Vector3.up * 2.6f, "OUT!", new Color(1f, 0.37f, 0.36f), 9f);
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
