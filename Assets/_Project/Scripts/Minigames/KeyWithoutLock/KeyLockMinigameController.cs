using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Key Hunt, musical chairs with keys: up to four doors glow, each in its own colour, but with N contestants only
/// N-1 of them have a real key hidden around the vault; the other doors are decoys. A key's colour says nothing about
/// its door (they never match): grab a key (E) and try it on the doors (E at a door). The wrong door keeps you out but
/// you keep the key; the right one lets you escape and shuts for good. Fake keys crumble at any door. Someone is
/// always left without a key, so shoving a key-holder knocks the key out of their hands. Escape order decides the
/// placement; the last one inside places last.
/// </summary>
public class KeyLockMinigameController : MinigameController
{
    /// <summary>The warehouse has its own walls.</summary>
    protected override bool UsesBoundary => false;

    private const byte CLOSED = 255;

    [SerializeField] private NetworkPrefabRef _keyPrefab;
    [SerializeField] private Transform _keySpawnArea;
    [SerializeField] private float _keySpawnRadius = 15f;
    [SerializeField] private float _tryRange = 2.6f;
    [SerializeField, Tooltip("Where escaped contestants wait")] private VaultDoor _door;

    [Networked] private int Escaped { get; set; }
    /// <summary>Per door (index into the sorted doors): its key colour while open, <see cref="CLOSED"/> once shut or unused.</summary>
    [Networked, Capacity(16)] private NetworkArray<byte> DoorColor { get; }

    private HuntDoor[] _doors = System.Array.Empty<HuntDoor>();
    private SpectatorLounge _deck;
    private readonly byte[] _shownColor = new byte[16];
    private readonly List<PlayerRef> _escapeOrder = new();
    private readonly List<Vector3> _spawnSpots = new();

    public override string TwistTitle => "MASTER KEY";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "A big golden key that opens ANY open door appears in the middle - for a few seconds.";

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier != ChaosModifier.Twist) return;
        Runner.Spawn(_keyPrefab, _keySpawnArea.position + Vector3.up * 3f, Quaternion.identity, null,
            (runner, spawned) =>
            {
                var key = spawned.GetComponent<VaultKey>();
                key.Variant = 0;
                key.DoorId = VaultKey.MASTER;
            });
    }

    protected override void OnChaosEnded(ChaosModifier modifier)
    {
        if (modifier != ChaosModifier.Twist) return;
        // The master key vanishes when the twist ends, even from someone's hands.
        for (int i = VaultKey.Active.Count - 1; i >= 0; i--)
            if (VaultKey.Active[i].IsMaster && VaultKey.Active[i].Object != null)
                Runner.Despawn(VaultKey.Active[i].Object);
    }

    public override void Spawned()
    {
        base.Spawned();
        _doors = FindObjectsByType<HuntDoor>(FindObjectsSortMode.None).OrderBy(d => d.Id).ToArray();
        // Escaped contestants watch the scramble from a glass deck above the vault.
        _deck = SpectatorLounge.Build(transform, _keySpawnArea.position + Vector3.up * 11f, _keySpawnArea.position + Vector3.forward, glass: true);
        for (int i = 0; i < _shownColor.Length; i++) _shownColor[i] = 254;   // force the first refresh
        if (HasStateAuthority)
            for (int i = 0; i < _doors.Length && i < 16; i++) DoorColor.Set(i, CLOSED);
    }

    private int DoorIndex(HuntDoor door) => System.Array.IndexOf(_doors, door);

    private int OpenDoors()
    {
        int open = 0;
        for (int i = 0; i < _doors.Length && i < 16; i++) if (DoorColor[i] != CLOSED) open++;
        return open;
    }

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        int open = OpenDoors();
        // What to do next, for the local contestant.
        var held = Player.Local != null && Player.Local.Interaction != null ? Player.Local.Interaction.HeldObject : null;
        string todo;
        if (held != null && held.TryGetComponent<VaultKey>(out var key))
        {
            todo = key.IsMaster
                ? "MASTER KEY! Press <b>E</b> at any open door"
                : "Try your key on the doors (<b>E</b>) - which one does it open?";
        }
        else
            todo = "Find a key (<b>E</b>) - or shove someone who has one!";
        return $"{todo}\n<size=80%>{open} door{(open == 1 ? "" : "s")} left, {PlayerCount - Escaped} still inside</size>";
    }

    /// <summary>Doors lit each round whatever the head count (one lit door with two players was a non-search).</summary>
    private const int LIT_DOORS = 4;

    /// <summary>Host only: doors (indices) that have a real key this round (one fewer than contestants).</summary>
    private readonly List<int> _realDoors = new();

    /// <summary>Host. During the rules card: up to four doors light up, each in its own colour.</summary>
    protected override void OnIntro()
    {
        base.OnIntro();
        int doors = Mathf.Min(_doors.Length, 16);
        int maxOpen = Mathf.Min(doors, VaultKey.Colors.Length);
        int realKeys = Mathf.Clamp(ConnectedPlayers().Count() - 1, 1, maxOpen);
        int openDoors = Mathf.Clamp(Mathf.Max(LIT_DOORS, realKeys), 1, maxOpen);
        var order = Enumerable.Range(0, doors).OrderBy(_ => Random.value).ToList();
        var colors = Enumerable.Range(0, VaultKey.Colors.Length).OrderBy(_ => Random.value).ToList();
        for (int i = 0; i < doors; i++) DoorColor.Set(i, CLOSED);
        for (int i = 0; i < openDoors; i++) DoorColor.Set(order[i], (byte)colors[i]);
        _realDoors.Clear();
        _realDoors.AddRange(order.Take(openDoors).OrderBy(_ => Random.value).Take(realKeys));
    }

    /// <summary>
    /// Host. A real key for N-1 of the lit doors and a fake for every lit door, hidden around the vault. Keys get random
    /// colours, never their own door's: the colour is only there to tell keys apart.
    /// </summary>
    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;

        int openDoors = OpenDoors();
        var spots = PickSpawnSpots(_realDoors.Count + openDoors);
        int spot = 0;
        void SpawnKey(byte doorId, byte variant) =>
            Runner.Spawn(_keyPrefab, spots[spot++], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null,
                (runner, spawned) =>
                {
                    var key = spawned.GetComponent<VaultKey>();
                    key.Variant = variant;
                    key.DoorId = doorId;
                });
        foreach (int door in _realDoors)
        {
            byte variant;
            do variant = (byte)Random.Range(0, VaultKey.Colors.Length); while (variant == DoorColor[door]);
            SpawnKey((byte)_doors[door].Id, variant);
        }
        for (int i = 0; i < openDoors; i++)
            SpawnKey(VaultKey.FAKE, (byte)Random.Range(0, VaultKey.Colors.Length));
        Debug.Log($"[Keys] {openDoors} open doors, {_realDoors.Count} real keys, {openDoors} fakes");
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
            if (_doors.Any(d => Vector3.Distance(d.FrontPosition, hit.point) < 2.5f) || _spawnSpots.Any(s => Vector3.Distance(s, hit.point) < 4f))
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

        // Keys knocked off the walkways or out of the room come back, so no door ever loses its key.
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

    public override void Render()
    {
        base.Render();
        // Doors glow in their key's colour while open and go dark once shut.
        for (int i = 0; i < _doors.Length && i < 16; i++)
        {
            byte color = DoorColor[i];
            if (color == _shownColor[i]) continue;
            _shownColor[i] = color;
            _doors[i].ShowState(color == CLOSED ? (Color?)null : VaultKey.Colors[color % VaultKey.Colors.Length]);
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
        int index = DoorIndex(door);
        bool open = index >= 0 && index < 16 && DoorColor[index] != CLOSED;
        if (open && key.DoorId == VaultKey.FAKE)
        {
            // A fake: it crumbles at the first door, go find a real one.
            Runner.Despawn(key.Object);
            RPC_Fake(door.Id);
            return;
        }
        if (!open || (key.DoorId != door.Id && !key.IsMaster))
        {
            // Not this door (or shut): keep the key and try another.
            RPC_Tried(door.Id, open, false);
            return;
        }

        // Through the door: it shuts for good, and a door opened by the master key takes its own key with it. One fake
        // goes too, so the share of fakes stays the same.
        DoorColor.Set(index, CLOSED);
        int doorId = door.Id;
        Runner.Despawn(key.Object);
        bool fakeRemoved = false;
        for (int i = VaultKey.Active.Count - 1; i >= 0; i--)
        {
            var other = VaultKey.Active[i];
            bool fake = other != null && other.DoorId == VaultKey.FAKE && !fakeRemoved && !other.Grabbable.IsGrabbed;
            if (other != null && other != key && other.Object != null && (other.DoorId == doorId || fake))
            {
                if (fake) fakeRemoved = true;
                if (other.Grabbable.IsGrabbed)
                    foreach (var entry in _playerSpawner.Players)
                        if (entry.Value != null && entry.Value.Interaction != null && entry.Value.Interaction.HeldObject == other.Object)
                            entry.Value.Interaction.ForceDrop();
                Runner.Despawn(other.Object);
            }
        }

        Escaped++;
        _escapeOrder.Add(who);
        Debug.Log($"[Keys] {who} escaped through door {door.Id} (#{_escapeOrder.Count})");
        RPC_Tried(door.Id, true, true);

        // Up to the glass deck over the vault (free to move, not a ghost: they escaped).
        if (_deck != null) _deck.Admit(player, spectator: false);
        else
        {
            player.ControlsLocked = true;
            _playerSpawner.Teleport(player, _door.EscapePosition + Random.insideUnitSphere.WithY(0f) * 1.5f);
        }

        var inside = ConnectedPlayers().Where(p => !_escapeOrder.Contains(p)).ToList();
        if (inside.Count <= 1 || OpenDoors() == 0)
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
    private void RPC_Fake(int doorId)
    {
        var door = _doors.FirstOrDefault(d => d.Id == doorId);
        if (door == null) return;
        Sfx.Play(Sound.Nope);
        door.PlayReject();
        WorldPopup.Show(door.FrontPosition + Vector3.up * 2.4f, "FAKE KEY!", new Color(1f, 0.6f, 0.2f), 9f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Tried(int doorId, NetworkBool doorOpen, NetworkBool escaped)
    {
        var door = _doors.FirstOrDefault(d => d.Id == doorId);
        if (door == null) return;
        Sfx.Play(escaped ? Sound.Escape : Sound.Nope);
        // In front of the door (its own position is inside the wall).
        var at = door.FrontPosition + Vector3.up * 2.4f;
        if (escaped)
        {
            door.PlayOpen();
            WorldPopup.Show(at, "ESCAPED!", new Color(0.4f, 1f, 0.5f), 9f);
            return;
        }
        door.PlayReject();
        WorldPopup.Show(at, doorOpen ? "WRONG DOOR" : "SHUT", new Color(1f, 0.37f, 0.36f), 8f);
    }
}
