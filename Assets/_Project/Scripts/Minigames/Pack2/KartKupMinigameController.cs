using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kart Kup: everyone drives a kart (W/S throttle and brake, A/D steer, Space hops) three laps round a track raised
/// above the studio. Yellow pads give a burst of speed; bumping rivals is fair game. Fall off and you're put back on
/// the track. Finishers place in order; at the bell the rest by laps and distance. Layouts: Oval, Clover, Long Oval.
/// Twist: TURBO, everyone gets a speed boost.
/// The track is a closed loop of points built the same way on every peer.
/// </summary>
public class KartKupMinigameController : RaceMinigameController
{
    private const int POINTS = 96, LAPS = 3;
    private const float WIDTH = 11f;

    [SerializeField] private float _height = 12f;

    public override VehicleKind Vehicle => VehicleKind.Kart;
    public override bool AllowShove => false;
    public override bool UsesDefaultControls => false;
    public override string[] Variants => new[] { "Oval", "Clover", "Long Oval" };
    public override string TwistTitle => "TURBO";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "Everyone gets a burst of speed.";

    /// <summary>Laps completed per player slot (for the HUD; the host keeps the exact progress).</summary>
    [Networked, Capacity(32)] private NetworkArray<byte> Laps { get; }

    private readonly List<Vector3> _points = new();
    private readonly Dictionary<PlayerRef, int> _index = new();
    private readonly Dictionary<PlayerRef, int> _lap = new();
    private readonly List<int> _pads = new();

    private Vector3 Centre => _centre.position + Vector3.up * _height;
    protected override float FallHeight => Centre.y - 5f;

    public override void Spawned()
    {
        base.Spawned();
        for (int i = 0; i < POINTS; i++) _points.Add(Point(i / (float)POINTS * Mathf.PI * 2f));
        BuildTrack();
    }

    private Vector3 Point(float t)
    {
        float x, z;
        switch (Variant)
        {
            case 1:
                float r = 24f + 6f * Mathf.Sin(3f * t);
                x = Mathf.Cos(t) * r * 1.15f; z = Mathf.Sin(t) * r;
                break;
            case 2:
                x = Mathf.Cos(t) * 44f; z = Mathf.Sin(t) * 15f;
                break;
            default:
                // A rounded rectangle (superellipse).
                float c = Mathf.Cos(t), s = Mathf.Sin(t);
                x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.6f) * 34f;
                z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.6f) * 19f;
                break;
        }
        return Centre + new Vector3(x, 0f, z);
    }

    private Vector3 Tangent(int i) => (_points[(i + 1) % POINTS] - _points[(i + POINTS - 1) % POINTS]).normalized;

    private void BuildTrack()
    {
        var asphalt = new Color(0.32f, 0.3f, 0.38f);
        for (int i = 0; i < POINTS; i++)
        {
            var a = _points[i];
            var b = _points[(i + 1) % POINTS];
            var mid = (a + b) * 0.5f;
            var dir = (b - a).normalized;
            float length = (b - a).magnitude + 1.2f;
            float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
            ArenaProps.Block(transform, PrimitiveType.Cube, mid + Vector3.down * 0.6f, new Vector3(WIDTH, 0.6f, length), asphalt, yaw);
            // A disc under every joint fills the wedge-shaped gaps on the outside of bends.
            ArenaProps.Block(transform, PrimitiveType.Cylinder, a + Vector3.down * 0.61f, new Vector3(WIDTH, 0.6f, WIDTH), asphalt);
            var normal = Vector3.Cross(Vector3.up, dir);
            foreach (float side in new[] { -1f, 1f })
                ArenaProps.Block(transform, PrimitiveType.Cube, mid + normal * side * (WIDTH * 0.5f + 0.3f), new Vector3(0.6f, 1.1f, length),
                    i % 2 == 0 ? new Color(0.95f, 0.3f, 0.3f) : Color.white, yaw);
        }
        // Start line and speed pads (no colliders: the host checks who drives over them).
        var line = ArenaProps.Block(transform, PrimitiveType.Cube, _points[0] + Vector3.up * 0.01f, new Vector3(WIDTH, 0.04f, 1.2f), Color.white,
            Quaternion.LookRotation(Tangent(0)).eulerAngles.y);
        Destroy(line.GetComponent<Collider>());
        for (int k = 1; k <= 3; k++)
        {
            int i = k * POINTS / 4 + POINTS / 12;
            _pads.Add(i);
            var pad = ArenaProps.Block(transform, PrimitiveType.Cube, _points[i] + Vector3.up * 0.01f, new Vector3(WIDTH * 0.45f, 0.05f, 3.2f), ArenaProps.Yellow,
                Quaternion.LookRotation(Tangent(i)).eulerAngles.y);
            Destroy(pad.GetComponent<Collider>());
        }
        ArenaProps.Done();
    }

    protected override Vector3 StartPosition(int slot, int count, out float yaw)
    {
        int row = slot / 2, column = slot % 2;
        int i = POINTS - 2 - row * 2;
        var tangent = Tangent(i);
        yaw = Quaternion.LookRotation(tangent).eulerAngles.y;
        return _points[i] + Vector3.Cross(Vector3.up, tangent) * (column == 0 ? -2.6f : 2.6f) + Vector3.up * 0.6f;
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        _index.Clear();
        _lap.Clear();
        for (int i = 0; i < 32; i++) Laps.Set(i, 0);
    }

    /// <summary>Nearest track point, searched near the last one (the track never crosses itself).</summary>
    private int Nearest(PlayerRef player, Vector3 position)
    {
        int start = _index.TryGetValue(player, out int last) ? last : -1;
        int best = 0;
        float bestDistance = float.MaxValue;
        int from = start < 0 ? 0 : start - 6, to = start < 0 ? POINTS : start + 7;
        for (int k = from; k < to; k++)
        {
            int i = ((k % POINTS) + POINTS) % POINTS;
            float d = (new Vector3(position.x, Centre.y, position.z) - _points[i]).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    /// <summary>Laps times points plus the point reached. The grid starts just behind the line on lap -1.</summary>
    protected override float Progress(PlayerRef player, Vector3 position)
    {
        int index = Nearest(player, position);
        if (!_lap.TryGetValue(player, out int lap)) lap = index > POINTS / 2 ? -1 : 0;
        if (_index.TryGetValue(player, out int last))
        {
            if (last > POINTS * 3 / 4 && index < POINTS / 4) lap++;
            else if (last < POINTS / 4 && index > POINTS * 3 / 4) lap--;
        }
        _index[player] = index;
        _lap[player] = lap;
        Laps.Set(player.PlayerId & 31, (byte)Mathf.Clamp(lap + 1, 0, 255));
        return lap * POINTS + index;
    }

    protected override bool IsFinished(float progress) => progress >= LAPS * POINTS;

    protected override Vector3 RespawnPoint(PlayerRef player, float bestProgress, out float yaw)
    {
        int i = Mathf.Clamp(_index.TryGetValue(player, out int last) ? last : 0, 0, POINTS - 1);
        yaw = Quaternion.LookRotation(Tangent(i)).eulerAngles.y;
        return _points[i] + Vector3.up * 0.6f;
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier != ChaosModifier.Twist) return;
        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null && entry.Value.TryGetComponent<PlayerMovement>(out var movement)) movement.Boost(4f);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || !_index.TryGetValue(entry.Key, out int index)) continue;
            foreach (int pad in _pads)
            {
                if (Mathf.Abs(index - pad) > 1) continue;
                var rel = player.transform.position - _points[pad];
                if (Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, Tangent(pad)))) < WIDTH * 0.25f && player.TryGetComponent<PlayerMovement>(out var movement) && !movement.IsBoosted)
                    movement.Boost(1.4f);
            }
        }
    }

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        int lap = Runner.LocalPlayer.IsRealPlayer ? Laps[Runner.LocalPlayer.PlayerId & 31] : 0;
        string mine = $"Lap <b>{Mathf.Clamp(lap, 1, LAPS)}</b> / {LAPS}";
        return FinishedCount > 0 ? $"{mine}   {FinishedCount} finished" : mine;
    }
}
