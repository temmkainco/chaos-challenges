using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kart Kup: everyone drives a kart (W/S throttle and brake, A/D steer, Space hops) round a race track raised above
/// the arena. Karts bounce off the barriers. Drive through item boxes for a BANANA (drop it behind you; whoever hits
/// it spins out), a BOOST, or a SHOCKWAVE (shoves every nearby kart away) and press E to use it. Finish order wins;
/// at the bell the rest by laps and distance. Layouts: Grand Prix, Chicane, Esses. Twist: TURBO.
/// The track is a loop of race-kit pieces on a 10 m grid (straights and 20 m corners, left or right) built the same
/// way on every peer; a list of waypoints along its middle measures everyone's progress.
/// </summary>
public class KartKupMinigameController : RaceMinigameController
{
    private const float CELL = 10f, WALL_HEIGHT = 1.3f, BOX_RESPAWN = 5f;
    private const int MAX_BANANAS = 12;
    public const byte NO_ITEM = 0, BANANA = 1, BOOST = 2, SHOCK = 3;

    [SerializeField] private float _height = 12f;

    public override VehicleKind Vehicle => VehicleKind.Kart;
    public override bool AllowShove => false;
    public override bool UsesDefaultControls => false;
    public override string[] Variants => new[] { "Grand Prix", "Chicane", "Esses", "Hairpins", "Serpent" };

    [SerializeField, Tooltip("A kart that hasn't moved this many metres for _unstuckSeconds is put back on the track")]
    private float _unstuckDistance = 1.5f;
    [SerializeField] private float _unstuckSeconds = 2.5f;

    /// <summary>The item the local player holds (0 none, then BANANA, BOOST, SHOCK), for the HUD slot.</summary>
    public byte LocalItem => Runner != null && Runner.LocalPlayer.IsRealPlayer ? Items[Runner.LocalPlayer.PlayerId & 31] : NO_ITEM;

    /// <summary>Host: where each kart was at the last stuck check, and since when it has hardly moved.</summary>
    private readonly Dictionary<PlayerRef, (Vector3 position, int since)> _stuck = new();
    public override string TwistTitle => "TURBO";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "Everyone gets a burst of speed.";

    /// <summary>Per player slot: the item in hand.</summary>
    [Networked, Capacity(32)] private NetworkArray<byte> Items { get; }
    /// <summary>Tick each item box was taken (0 = there).</summary>
    [Networked, Capacity(16)] private NetworkArray<int> BoxTaken { get; }
    /// <summary>Bananas on the track (y below -9000 = none) and who dropped each.</summary>
    [Networked, Capacity(MAX_BANANAS)] private NetworkArray<Vector3> Bananas { get; }

    private static readonly string[] Layouts = { "SSSSSLSSLSSSSSLSSL", "SSSRSSSSRRLLSRRSSRLR", "SSLRLSSLSLSSRSSLLS", "SLLSRSSLSLSLRSLS", "SLRSSLLSSSRLLSSSSSSL" };
    private static readonly Vector2Int[] Headings = { new(1, 0), new(0, 1), new(-1, 0), new(0, -1) };   // E N W S

    private readonly List<Vector3> _points = new();     // waypoints along the middle of the track, in driving order
    private readonly List<Vector3> _boxes = new();
    private readonly Dictionary<PlayerRef, int> _index = new();
    private readonly Dictionary<PlayerRef, int> _lap = new();
    private readonly Dictionary<PlayerRef, int> _hitTick = new();
    private readonly Transform[] _bananaViews = new Transform[MAX_BANANAS];
    private Transform[] _boxViews;
    private Vector3 _origin;
    private int Laps => Variant == 0 ? 3 : 2;

    private int Count => _points.Count;
    protected override float FallHeight => _origin.y - 5f;
    private static Vector3 Dir(int heading) => new(Headings[heading].x, 0f, Headings[heading].y);

    public override void Spawned()
    {
        base.Spawned();
        _origin = _centre.position + Vector3.up * _height;
        Build(Layouts[Variant % Layouts.Length]);
        KartItemSlot.Create(this);
    }

    /// <summary>Lays the pieces, barriers and waypoints; the track is then shifted so it sits over the arena centre.</summary>
    private void Build(string layout)
    {
        var pieces = new List<(bool corner, Vector2Int cell, int heading, int exit, bool right)>();
        var cells = new List<Vector2Int>();
        var at = Vector2Int.zero;
        int h = 0;
        foreach (char c in layout)
        {
            if (c == 'S')
            {
                pieces.Add((false, at, h, h, false));
                cells.Add(at);
                at += Headings[h];
                continue;
            }
            bool right = c == 'R';
            int side = right ? (h + 3) % 4 : (h + 1) % 4;
            pieces.Add((true, at, h, side, right));
            var exitCell = at + Headings[h] + Headings[side];
            cells.Add(at); cells.Add(at + Headings[h]); cells.Add(at + Headings[side]); cells.Add(exitCell);
            at = exitCell + Headings[side];
            h = side;
        }
        var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
        foreach (var cell in cells) { min = Vector2.Min(min, cell); max = Vector2.Max(max, cell); }
        var middle = (min + max) * 0.5f * CELL;
        _origin -= new Vector3(middle.x, 0f, middle.y);
        Vector3 Centre(Vector2Int cell) => _origin + new Vector3(cell.x * CELL, 0f, cell.y * CELL);

        foreach (var (corner, cell, heading, exit, right) in pieces)
        {
            var entry = Centre(cell);
            if (!corner)
            {
                Floor(entry, CELL, CELL);
                ArenaModels.Spawn("TrackStraight", transform, entry + Vector3.down * 0.1f, Quaternion.Euler(0f, heading % 2 == 0 ? 90f : 0f, 0f), new Vector3(CELL, 0.2f, CELL));
                var across = Vector3.Cross(Vector3.up, Dir(heading));
                foreach (float s in new[] { -1f, 1f }) Wall(entry + across * s * (CELL * 0.5f + 0.4f), Dir(heading), CELL);
                _points.Add(entry - Dir(heading) * (CELL * 0.25f));
                _points.Add(entry + Dir(heading) * (CELL * 0.25f));
                continue;
            }
            // A 2x2-cell corner: the arc's centre is the back corner of the block on the turning side.
            var fwd = Dir(heading);
            var side = Dir(exit);
            var block = entry + (fwd + side) * (CELL * 0.5f);
            Floor(block, CELL * 2f, CELL * 2f);
            // The model turns right from heading north at yaw 0; a left turn is the same piece driven backwards.
            int entryHeading = right ? heading : (heading + 3) % 4;
            ArenaModels.Spawn("TrackCorner", transform, block + Vector3.down * 0.1f, Quaternion.Euler(0f, entryHeading switch { 1 => 0f, 0 => 90f, 3 => 180f, _ => 270f }, 0f),
                new Vector3(CELL * 2f, 0.2f, CELL * 2f));
            var pivot = entry - fwd * (CELL * 0.5f) + side * (CELL * 1.5f);
            for (int k = 0; k <= 5; k++)
            {
                float a = k / 6f * Mathf.PI * 0.5f;
                // From the entry (pointing from the pivot back to the entry side) round to the exit.
                var radial = -side * Mathf.Cos(a) + fwd * Mathf.Sin(a);
                if (k < 5) _points.Add(pivot + radial * (CELL * 1.5f));
            }
            // Barriers: an outer arc and a short inner arc, as short overlapping straight segments (no gaps at the joins).
            const int ARC = 12;
            for (int k = 0; k < ARC; k++)
            {
                float a0 = k / (float)ARC * Mathf.PI * 0.5f, a1 = (k + 1) / (float)ARC * Mathf.PI * 0.5f;
                foreach (float radius in new[] { CELL * 2f + 0.4f, CELL - 0.4f })
                {
                    var p0 = pivot + (-side * Mathf.Cos(a0) + fwd * Mathf.Sin(a0)) * radius;
                    var p1 = pivot + (-side * Mathf.Cos(a1) + fwd * Mathf.Sin(a1)) * radius;
                    Wall((p0 + p1) * 0.5f, (p1 - p0).normalized, (p1 - p0).magnitude + 0.5f);
                }
            }
        }

        // Start/finish gate, item boxes on two straights, scenery.
        var start = _points[0];
        var startDir = (_points[1] - _points[0]).normalized;
        ArenaModels.Spawn("GateFinish", transform, start + Vector3.up * 3.6f, Quaternion.LookRotation(startDir), new Vector3(CELL + 1.5f, 7f, 1.2f), true);
        for (int i = Count / 3; i < Count && _boxes.Count < 8; i += Count / 3)
        {
            var dir = (_points[(i + 1) % Count] - _points[i]).normalized;
            var across = Vector3.Cross(Vector3.up, dir);
            for (int k = -1; k <= 1; k += 2) _boxes.Add(_points[i] + across * k * 2.4f + Vector3.up * 1f);
        }
        _boxViews = new Transform[_boxes.Count];
        for (int b = 0; b < _boxes.Count; b++)
            _boxViews[b] = ArenaModels.Spawn("ItemBox", transform, _boxes[b], Quaternion.identity, new Vector3(1.4f, 1.4f, 1.4f), true);
        ArenaProps.Done();
    }

    private void Floor(Vector3 centre, float x, float z)
    {
        var floor = ArenaProps.Block(transform, PrimitiveType.Cube, centre + Vector3.down * 0.6f, new Vector3(x, 0.6f, z), Color.grey);
        floor.GetComponent<Renderer>().enabled = false;
    }

    private void Wall(Vector3 centre, Vector3 along, float length)
    {
        var wall = ArenaProps.Block(transform, PrimitiveType.Cube, centre, new Vector3(0.8f, WALL_HEIGHT, length), Color.white, Quaternion.LookRotation(along).eulerAngles.y);
        ArenaProps.Skin(wall, "BarrierWall", yawOffset: 90f);
    }

    protected override Vector3 StartPosition(int slot, int count, out float yaw)
    {
        var forward = (_points[1] - _points[0]).normalized;
        yaw = Quaternion.LookRotation(forward).eulerAngles.y;
        int row = slot / 2, column = slot % 2;
        return _points[0] - forward * (3f + row * 3.5f) + Vector3.Cross(Vector3.up, forward) * (column == 0 ? -2.2f : 2.2f) + Vector3.up * 0.6f;
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        _index.Clear();
        _lap.Clear();
        for (int i = 0; i < 32; i++) Items.Set(i, NO_ITEM);
        for (int i = 0; i < 16; i++) BoxTaken.Set(i, 0);
        for (int i = 0; i < MAX_BANANAS; i++) Bananas.Set(i, Vector3.down * 10000f);
    }

    private int Nearest(PlayerRef player, Vector3 position)
    {
        bool known = _index.TryGetValue(player, out int last);
        int best = 0;
        float bestDistance = float.MaxValue;
        int from = known ? last - 3 : 0, to = known ? last + 5 : Count;
        for (int k = from; k < to; k++)
        {
            int i = (k % Count + Count) % Count;
            float d = (position.WithY(0f) - _points[i].WithY(0f)).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    /// <summary>Laps times waypoints plus the waypoint reached. The grid starts behind the line on lap -1.</summary>
    protected override float Progress(PlayerRef player, Vector3 position)
    {
        int index = Nearest(player, position);
        if (!_lap.TryGetValue(player, out int lap)) lap = Vector3.Dot(position - _points[0], _points[1] - _points[0]) < 0f ? -1 : 0;
        if (_index.TryGetValue(player, out int last))
        {
            if (last > Count * 3 / 4 && index < Count / 4) lap++;
            else if (last < Count / 4 && index > Count * 3 / 4) lap--;
        }
        _index[player] = index;
        _lap[player] = lap;
        return lap * Count + index;
    }

    protected override bool IsFinished(float progress) => progress >= Laps * Count;
    protected override string ProgressLabel(float progress) => $"Lap {Mathf.Clamp(Mathf.FloorToInt(progress / Mathf.Max(1, Count)) + 1, 1, Laps)}/{Laps}";

    protected override Vector3 RespawnPoint(PlayerRef player, float bestProgress, out float yaw)
    {
        int i = _index.TryGetValue(player, out int last) ? last : 0;
        yaw = Quaternion.LookRotation(_points[(i + 1) % Count] - _points[i]).eulerAngles.y;
        return _points[i] + Vector3.up * 0.6f;
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier != ChaosModifier.Twist) return;
        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null && entry.Value.TryGetComponent<PlayerMovement>(out var movement)) movement.Boost(4f);
    }

    /// <summary>Host. E uses the item in hand.</summary>
    public override bool HandlePrimaryAction(Player player)
    {
        if (!IsGameActive) return false;
        var who = player.Object.InputAuthority;
        int slot = who.PlayerId & 31;
        byte item = Items[slot];
        if (item == NO_ITEM) return true;
        Items.Set(slot, NO_ITEM);
        var movement = player.GetComponent<PlayerMovement>();
        switch (item)
        {
            case BANANA:
                for (int i = 0; i < MAX_BANANAS; i++)
                    if (Bananas[i].y < -9000f) { Bananas.Set(i, player.transform.position - player.transform.forward * 2.2f); break; }
                _hitTick[who] = Runner.Tick;   // your own banana can't get you straight away
                break;
            case BOOST:
                movement.Boost(2.2f);
                break;
            case SHOCK:
                foreach (var entry in _playerSpawner.Players)
                {
                    if (entry.Value == null || entry.Key == who) continue;
                    var away = entry.Value.transform.position - player.transform.position;
                    if (away.magnitude > 8f || !entry.Value.TryGetComponent<PlayerMovement>(out var other)) continue;
                    other.Launch(away.WithY(0f).normalized * 12f + Vector3.up * 5f);
                }
                RPC_Shock(player.transform.position);
                break;
        }
        return true;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        int respawn = Mathf.CeilToInt(BOX_RESPAWN * Runner.TickRate);
        for (int b = 0; b < _boxes.Count && b < 16; b++)
            if (BoxTaken[b] != 0 && Runner.Tick - BoxTaken[b] > respawn) BoxTaken.Set(b, 0);

        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || player.ControlsLocked) continue;
            var position = player.transform.position;
            int slot = entry.Key.PlayerId & 31;
            // Wedged against a barrier or another kart: back on the track after a moment.
            if (Runner.Tick % (Runner.TickRate / 2) == 0)
            {
                if (!_stuck.TryGetValue(entry.Key, out var stuck) || (position - stuck.position).magnitude > _unstuckDistance)
                    _stuck[entry.Key] = (position, Runner.Tick);
                else if ((Runner.Tick - stuck.since) * Runner.DeltaTime >= _unstuckSeconds && !HasFinished(entry.Key))
                {
                    _stuck.Remove(entry.Key);
                    SendToCheckpoint(entry.Key);
                    RPC_Unstuck(position);
                    continue;
                }
            }
            // Item boxes.
            for (int b = 0; b < _boxes.Count && b < 16; b++)
            {
                if (BoxTaken[b] != 0 || (position - _boxes[b]).magnitude > 2.2f) continue;
                BoxTaken.Set(b, Runner.Tick);
                if (Items[slot] == NO_ITEM) Items.Set(slot, (byte)Random.Range(BANANA, SHOCK + 1));
                RPC_Box(_boxes[b], entry.Key);
            }
            // Bananas spin you out.
            if (_hitTick.TryGetValue(entry.Key, out int last) && Runner.Tick - last < Runner.TickRate) continue;
            for (int i = 0; i < MAX_BANANAS; i++)
            {
                var banana = Bananas[i];
                if (banana.y < -9000f || (position - banana).WithY(0f).magnitude > 1.4f || Mathf.Abs(position.y - banana.y) > 1.5f) continue;
                Bananas.Set(i, Vector3.down * 10000f);
                _hitTick[entry.Key] = Runner.Tick;
                if (player.TryGetComponent<PlayerMovement>(out var movement)) movement.Launch(Vector3.up * 5f + player.transform.right * 2f);
                RPC_Slip(position, entry.Key);
                break;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Box(Vector3 position, PlayerRef player)
    {
        if (player == Runner.LocalPlayer) Sfx.Play(Sound.Pop, 1f, 0.1f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Slip(Vector3 position, PlayerRef player)
    {
        Sfx.Play(Sound.Whoosh, 1f, 0.1f);
        WorldPopup.Show(position + Vector3.up * 2.4f, "SLIP!", new Color(1f, 0.85f, 0.2f), 8f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Shock(Vector3 position)
    {
        Sfx.Play(Sound.Boom, 0.7f, 0.1f);
        LowPolyExplosion.Play(position + Vector3.up, 2.5f, Resources.Load<Material>("ThrowArc"));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Unstuck(Vector3 position) => WorldPopup.Show(position + Vector3.up * 2.5f, "UNSTUCK!", new Color(0.4f, 0.8f, 1f), 7f);

    /// <summary>The item now shows in its own HUD slot (<see cref="KartItemSlot"/>).</summary>
    public override string GetStatusText() => null;

    public override void Render()
    {
        base.Render();
        if (_boxViews == null) return;
        float spin = Time.time * 90f;
        for (int b = 0; b < _boxViews.Length && b < 16; b++)
        {
            if (_boxViews[b] == null) continue;
            bool there = !IsGameActive || BoxTaken[b] == 0;
            if (_boxViews[b].gameObject.activeSelf != there) _boxViews[b].gameObject.SetActive(there);
            _boxViews[b].rotation = Quaternion.Euler(0f, spin, 0f);
        }
        for (int i = 0; i < MAX_BANANAS; i++)
        {
            var banana = IsGameActive ? Bananas[i] : Vector3.down * 10000f;
            bool on = banana.y > -9000f;
            if (on && _bananaViews[i] == null) _bananaViews[i] = ArenaModels.Spawn("Banana", transform, banana, Quaternion.identity, new Vector3(1f, 0.7f, 1f), true);
            if (_bananaViews[i] == null) continue;
            if (_bananaViews[i].gameObject.activeSelf != on) _bananaViews[i].gameObject.SetActive(on);
            if (on) _bananaViews[i].position = banana + Vector3.up * 0.3f;
        }
    }
}
