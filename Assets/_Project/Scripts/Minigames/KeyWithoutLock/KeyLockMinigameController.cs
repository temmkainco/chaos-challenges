using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Key Without Lock: keys lie all over the arena, but only a few fit the vault door, and nobody knows which.
/// Grab a key (E), carry it to the door and press E there to try it. A dud shatters; a good key lets you
/// escape (and is used up). Escape order decides the placement; whoever is still inside when the last
/// good key is used, or when time runs out, loses.
/// </summary>
public class KeyLockMinigameController : MinigameController
{
    [SerializeField] private NetworkPrefabRef _keyPrefab;
    [SerializeField] private Transform _keySpawnArea;
    [SerializeField] private float _keySpawnRadius = 15f;
    [SerializeField, Tooltip("Keys per player (plus two)")] private int _keysPerPlayer = 3;
    [SerializeField] private float _tryRange = 4f;
    [SerializeField] private VaultDoor _door;

    [Networked] private int KeysLeft { get; set; }
    [Networked] private int GoodKeysLeft { get; set; }
    [Networked] private int Escaped { get; set; }

    private readonly HashSet<NetworkId> _goodKeys = new();
    private readonly List<PlayerRef> _escapeOrder = new();
    private readonly List<Vector3> _spawnSpots = new();

    public override string GetStatusText() =>
        $"{GoodKeysLeft} good {(GoodKeysLeft == 1 ? "key" : "keys")} hidden among {KeysLeft}";

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        int players = ConnectedPlayers().Count();
        int keyCount = players * _keysPerPlayer + 2;
        int goodCount = Mathf.Max(1, players - 1);

        var spots = PickSpawnSpots(keyCount);
        var goodIndices = new HashSet<int>(Enumerable.Range(0, keyCount).OrderBy(_ => Random.value).Take(goodCount));
        for (int i = 0; i < keyCount; i++)
        {
            bool good = goodIndices.Contains(i);
            var obj = Runner.Spawn(_keyPrefab, spots[i], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null,
                (runner, spawned) => spawned.GetComponent<VaultKey>().Variant = (byte)Random.Range(0, 5));
            if (good) _goodKeys.Add(obj.Id);
        }

        KeysLeft = keyCount;
        GoodKeysLeft = goodCount;
        Debug.Log($"[Keys] {keyCount} keys, {goodCount} good");
    }

    /// <summary>Random spots on the arena floor, spread out and clear of obstacles.</summary>
    private List<Vector3> PickSpawnSpots(int count)
    {
        _spawnSpots.Clear();
        var centre = _keySpawnArea.position;
        var physics = Runner.GetPhysicsScene();
        for (int attempt = 0; attempt < count * 40 && _spawnSpots.Count < count; attempt++)
        {
            var flat = Random.insideUnitCircle * _keySpawnRadius;
            var probe = centre + new Vector3(flat.x, 6f, flat.y);
            if (!physics.Raycast(probe, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            // Only the arena floor, not on top of crates or the door.
            if (Mathf.Abs(hit.point.y - centre.y) > 0.4f)
                continue;
            if (Vector3.Distance(hit.point, _door.FrontPosition) < 6f || _spawnSpots.Any(s => Vector3.Distance(s, hit.point) < 2.5f))
                continue;
            _spawnSpots.Add(hit.point + Vector3.up * 0.5f);
        }
        while (_spawnSpots.Count < count)
            _spawnSpots.Add(centre + Vector3.up * 0.5f + Random.insideUnitSphere.normalized * 2f);
        return _spawnSpots;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive || Runner.Tick % 30 != 0)
            return;

        // Keys thrown off the stage come back, so a good key can never be lost for good.
        foreach (var key in FindObjectsByType<VaultKey>(FindObjectsSortMode.None))
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

        var toDoor = _door.FrontPosition - player.transform.position;
        toDoor.y = 0f;
        if (toDoor.magnitude > _tryRange)
            return false;

        TryKey(player, key);
        return true;
    }

    private void TryKey(Player player, VaultKey key)
    {
        var who = player.Object.InputAuthority;
        bool good = _goodKeys.Remove(key.Object.Id);
        KeysLeft--;
        Runner.Despawn(key.Object);

        if (!good)
        {
            Debug.Log($"[Keys] {who} tried a dud");
            RPC_Tried(player.transform.position, false);
            return;
        }

        GoodKeysLeft--;
        Escaped++;
        _escapeOrder.Add(who);
        Debug.Log($"[Keys] {who} escaped (#{_escapeOrder.Count})");
        RPC_Tried(player.transform.position, true);

        player.ControlsLocked = true;
        _playerSpawner.Teleport(player, _door.EscapePosition + Random.insideUnitSphere.WithY(0f) * 1.5f);

        var inside = ConnectedPlayers().Where(p => !_escapeOrder.Contains(p)).ToList();
        if (inside.Count <= 1 || GoodKeysLeft == 0)
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
    private void RPC_Tried(Vector3 position, NetworkBool good)
    {
        Sfx.Play(good ? Sound.Escape : Sound.Nope);
        if (good)
        {
            _door.PlayOpen();
            WorldPopup.Show(_door.FrontPosition + Vector3.up * 3.5f, "ESCAPED!", new Color(0.4f, 1f, 0.5f), 9f);
        }
        else
        {
            _door.PlayReject();
            WorldPopup.Show(position + Vector3.up * 2.8f, "Nope!", new Color(1f, 0.45f, 0.35f), 7f);
        }
    }
}
