using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Bomb Rush: conveyors keep feeding bombs into the arena. Grab one (E), throw it (E again) at a rival:
/// thrown bombs go off on contact, lit ones on their own after the fuse. A bomb going off in your hands or right
/// next to you knocks you out; farther away it launches you, and whoever falls off the arena is out too.
/// Knocked-out contestants watch from the <see cref="SpectatorLounge"/>. Last contestant standing wins; on time-up
/// the survivors tie.
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
        BuildLayout();
    }

    public override string[] Variants => new[] { "Open Floor", "Crates", "Pillars" };

    /// <summary>Cover in the middle of the arena: it blocks thrown bombs (not the blast).</summary>
    private void BuildLayout()
    {
        var ground = _arenaCentre;
        if (Variant == 1)
        {
            var crates = new[] { new Vector3(-4.5f, 0f, 2f), new Vector3(3.5f, 0f, 4.5f), new Vector3(5f, 0f, -3f), new Vector3(-2f, 0f, -5.5f), new Vector3(0.5f, 0f, 0.5f) };
            for (int i = 0; i < crates.Length; i++)
                ArenaProps.Block(transform, PrimitiveType.Cube, ground + crates[i], new Vector3(1.8f, 1.4f, 1.8f), new Color(0.78f, 0.55f, 0.32f), i * 23f);
        }
        else if (Variant == 2)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = (i * 90f + 45f) * Mathf.Deg2Rad;
                ArenaProps.Block(transform, PrimitiveType.Cylinder, ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 6.5f,
                    new Vector3(1.8f, 3.5f, 1.8f), i % 2 == 0 ? ArenaProps.Blue : ArenaProps.Pink);
            }
        }
        ArenaProps.Done();
    }

    public ConveyorBelt GetBelt(int index) => index >= 0 && index < _belts.Length ? _belts[index] : null;

    private bool SuddenDeath => IsTimeLimited && IsGameActive && TimeRemaining <= _suddenDeathSeconds;

    public override string TwistTitle => "BOMB BONANZA";
    public override string TwistDescription => "Bombs rain down from the sky all over the arena.";

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
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        // Leaving the arena (onto its raised rim, or down) is out: no hiding outside and jumping back in.
        if (HasStateAuthority && IsGameActive && Runner.Tick % 10 == 0)
        {
            float floor = _arenaCentre.y - 1f, rim = _arenaCentre.y + 0.6f;
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (!_playerSpawner.Players.TryGet(_alive[i], out var p) || p == null) continue;
                float y = p.transform.position.y;
                bool onRim = y > rim && p.TryGetComponent<PlayerMovement>(out var movement) && movement.IsGrounded && !ArenaProps.IsStandingOnProp(p.transform.position);
                if (y < floor || onRim)
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
        if (!HasStateAuthority || !IsGameActive || _belts.Length == 0)
            return;

        if (!SpawnTimer.Expired(Runner))
            return;

        float interval = _spawnInterval * 2f / Mathf.Max(2, _alive.Count) * (SuddenDeath ? 0.5f : 1f) * (TwistActive ? 0.4f : 1f);
        SpawnTimer = TickTimer.CreateFromSeconds(Runner, interval);

        if (Bomb.Active.Count >= _alive.Count * _maxBombsPerPlayer * (SuddenDeath || TwistActive ? 2 : 1))
            return;

        // Bomb Bonanza: bombs rain from the sky at random spots across the arena instead of out of the conveyors.
        if (TwistActive)
        {
            var flat = Random.insideUnitCircle * 15f;
            Runner.Spawn(_bombPrefab, _arenaCentre + new Vector3(flat.x, 14f, flat.y), Quaternion.identity, null,
                (runner, obj) => { var bomb = obj.GetComponent<Bomb>(); bomb.Initialize(this, -1); bomb.FromSky = true; });
            return;
        }

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
