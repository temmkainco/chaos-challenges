using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Key Hunt: a needle in a haystack. The vault has many numbered doors and many more keys; every key opens exactly
/// one door and nobody knows which. Carry a key (E) to any door and press E to try it. The right door opens and you
/// escape; a wrong door leaves the key in your hand and tells you how close its real door is (the key turns
/// cold / warm / hot), so searching is a skill. Keys cannot be thrown, and a key you hold is yours: shoves do not
/// knock it loose. Escape order decides the placement; the last one inside (or everyone left when time runs out)
/// places last.
/// </summary>
public class KeyLockMinigameController : MinigameController
{
    [SerializeField] private NetworkPrefabRef _keyPrefab;
    [SerializeField] private Transform _keySpawnArea;
    [SerializeField] private float _keySpawnRadius = 15f;
    [SerializeField, Tooltip("Keys per door")] private int _keysPerDoor = 4;
    [SerializeField] private float _tryRange = 2.6f;
    [SerializeField, Tooltip("Where escaped contestants wait")] private VaultDoor _door;
    [SerializeField] private float _hotDistance = 10f;
    [SerializeField] private float _warmDistance = 22f;

    [Networked] private int Escaped { get; set; }

    private HuntDoor[] _doors = System.Array.Empty<HuntDoor>();
    private readonly List<PlayerRef> _escapeOrder = new();
    private readonly List<Vector3> _spawnSpots = new();

    public override bool ShoveDropsHeldItem => false;

    public override void Spawned()
    {
        base.Spawned();
        _doors = FindObjectsByType<HuntDoor>(FindObjectsSortMode.None).OrderBy(d => d.Id).ToArray();
    }

    public override string GetStatusText()
    {
        int inside = PlayerCount - Escaped;
        return $"{_doors.Length} doors - find the one your key opens\n<size=80%>{Escaped} escaped, {inside} still inside</size>";
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        int keyCount = _doors.Length * _keysPerDoor;
        var spots = PickSpawnSpots(keyCount);
        for (int i = 0; i < keyCount; i++)
        {
            byte doorId = (byte)_doors[i % _doors.Length].Id;
            byte variant = (byte)Random.Range(0, VaultKey.Colors.Length);
            Runner.Spawn(_keyPrefab, spots[i], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null,
                (runner, spawned) =>
                {
                    var key = spawned.GetComponent<VaultKey>();
                    key.Variant = variant;
                    key.DoorId = doorId;
                });
        }
        Debug.Log($"[Keys] {_doors.Length} doors, {keyCount} keys");
    }

    /// <summary>Random spots on any floor (ground and walkways) inside the vault, spread out.</summary>
    private List<Vector3> PickSpawnSpots(int count)
    {
        _spawnSpots.Clear();
        var centre = _keySpawnArea.position;
        var physics = Runner.GetPhysicsScene();
        for (int attempt = 0; attempt < count * 40 && _spawnSpots.Count < count; attempt++)
        {
            var flat = Random.insideUnitCircle * _keySpawnRadius;
            var probe = centre + new Vector3(flat.x, 9f, flat.y);
            if (!physics.Raycast(probe, Vector3.down, out var hit, 16f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            if (hit.normal.y < 0.8f || hit.point.y < centre.y - 0.5f || hit.point.y > centre.y + 3.5f)
                continue;
            if (_doors.Any(d => Vector3.Distance(d.FrontPosition, hit.point) < 2.5f) || _spawnSpots.Any(s => Vector3.Distance(s, hit.point) < 1.6f))
                continue;
            _spawnSpots.Add(hit.point + Vector3.up * 0.5f);
        }
        while (_spawnSpots.Count < count)
            _spawnSpots.Add(centre + Vector3.up * 0.5f + Random.insideUnitSphere.WithY(0f) * 6f);
        return _spawnSpots;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive || Runner.Tick % 30 != 0)
            return;

        // Keys knocked off the walkways or out of the room come back, so no door ever runs out of keys.
        foreach (var key in VaultKey.Active)
        {
            if (key.Grabbable.IsGrabbed || key.transform.position.y > _keySpawnArea.position.y - 4f)
                continue;

            var flat = Random.insideUnitCircle * _keySpawnRadius * 0.6f;
            var body = key.GetComponent<Fusion.Addons.Physics.NetworkRigidbody3D>();
            body.Teleport(_keySpawnArea.position + new Vector3(flat.x, 3f, flat.y));
            body.Rigidbody.linearVelocity = Vector3.zero;
        }
    }

    public override bool HandlePrimaryAction(Player player)
    {
        if (!IsGameActive)
            return false;

        var held = player.Interaction != null ? player.Interaction.HeldObject : null;
        if (held == null || !held.TryGetComponent<VaultKey>(out var key))
            return false;

        var door = NearestDoor(player.transform.position);
        if (door == null)
            return false;   // not at a door: E drops the key as usual

        TryKey(player, key, door);
        return true;
    }

    private HuntDoor NearestDoor(Vector3 position)
    {
        HuntDoor best = null;
        float bestDistance = _tryRange;
        foreach (var door in _doors)
        {
            var to = door.FrontPosition - position;
            if (Mathf.Abs(to.y) > 1.8f) continue;
            to.y = 0f;
            if (to.magnitude < bestDistance)
            {
                best = door;
                bestDistance = to.magnitude;
            }
        }
        return best;
    }

    private void TryKey(Player player, VaultKey key, HuntDoor door)
    {
        var who = player.Object.InputAuthority;
        if (key.DoorId != door.Id)
        {
            // Wrong door: the key stays yours and tells you how close its own door is.
            var target = _doors.FirstOrDefault(d => d.Id == key.DoorId);
            float distance = target != null ? Vector3.Distance(target.transform.position, door.transform.position) : 99f;
            key.Heat = (byte)(distance <= _hotDistance ? 3 : distance <= _warmDistance ? 2 : 1);
            RPC_Tried(door.Id, key.Heat, false);
            return;
        }

        Runner.Despawn(key.Object);
        Escaped++;
        _escapeOrder.Add(who);
        Debug.Log($"[Keys] {who} escaped through door {door.Id} (#{_escapeOrder.Count})");
        RPC_Tried(door.Id, 0, true);

        player.ControlsLocked = true;
        _playerSpawner.Teleport(player, _door.EscapePosition + Random.insideUnitSphere.WithY(0f) * 1.5f);

        var inside = ConnectedPlayers().Where(p => !_escapeOrder.Contains(p)).ToList();
        if (inside.Count <= 1)
            FinishRound(GetStandings(inside));
    }

    public override void HandlePlayerLeft(PlayerRef player)
    {
        if (!IsGameActive) return;
        var inside = ConnectedPlayers().Where(p => !_escapeOrder.Contains(p)).ToList();
        if (inside.Count <= 1)
            FinishRound(GetStandings(inside));
    }

    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings() =>
        GetStandings(ConnectedPlayers().Where(p => !_escapeOrder.Contains(p)).ToList());

    /// <summary>Escapers in order, then everyone still inside tied.</summary>
    private IReadOnlyList<IReadOnlyList<PlayerRef>> GetStandings(List<PlayerRef> inside)
    {
        var standings = _escapeOrder.Select(p => (IReadOnlyList<PlayerRef>)new[] { p }).ToList();
        if (inside.Count > 0)
            standings.Add(inside);
        return standings;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Tried(int doorId, byte heat, NetworkBool opened)
    {
        var door = _doors.FirstOrDefault(d => d.Id == doorId);
        if (door == null) return;
        Sfx.Play(opened ? Sound.Escape : Sound.Nope);
        var at = door.transform.position + Vector3.up * 4.2f;
        if (opened)
        {
            door.PlayOpen();
            WorldPopup.Show(at, "ESCAPED!", new Color(0.4f, 1f, 0.5f), 9f);
            return;
        }
        door.PlayReject();
        var (text, color) = heat switch
        {
            3 => ("HOT!", new Color(1f, 0.25f, 0.2f)),
            2 => ("WARM", new Color(1f, 0.6f, 0.2f)),
            _ => ("COLD", new Color(0.4f, 0.7f, 1f)),
        };
        WorldPopup.Show(at, text, color, 8f);
    }
}
