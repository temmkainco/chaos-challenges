using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Door Dash (after Fall Guys): race down a long hall through rows of doors. Real doors burst open when you run into
/// them; fakes are solid, so guess, bounce, and try the next. Finishers place in order; at the bell the rest are
/// ranked by how far they got. Layouts: Classic (3 of 5 real), Fake Out (2 of 5), Long Hall (more rows).
/// Twist: LOCKDOWN, every burst door slams shut again.
/// </summary>
public class DoorDashMinigameController : RaceMinigameController
{
    private const int DOORS = 5;
    private const float DOOR_WIDTH = 3f, ROW_GAP = 9f, DOOR_HEIGHT = 3.2f, WALL_HEIGHT = 5.5f, START_LENGTH = 8f;
    private const byte FAKE = 0, REAL = 1, BROKEN = 2;

    [SerializeField] private float _height = 4f;

    [Networked, Capacity(64)] private NetworkArray<byte> DoorState { get; }

    public override string[] Variants => new[] { "Classic", "Fake Out", "Long Hall" };
    public override string TwistTitle => "LOCKDOWN";
    public override string TwistDescription => "Every burst door slams shut again.";

    private int Rows => Variant == 2 ? 9 : 6;
    private float Width => DOORS * DOOR_WIDTH;
    private Vector3 Origin => _centre.position + Vector3.up * _height + Vector3.back * 30f;   // start line, hall runs along +Z
    private float RowZ(int row) => START_LENGTH + (row + 1) * ROW_GAP;
    private float FinishZ => RowZ(Rows - 1) + ROW_GAP;
    protected override float FallHeight => Origin.y - 2.5f;

    private Transform[] _doors;
    private Collider[] _doorColliders;
    private byte[] _shown;

    public override void Spawned()
    {
        base.Spawned();
        float length = FinishZ + 8f;
        // Floor, side walls and a finish pad.
        ArenaProps.Block(transform, PrimitiveType.Cube, Origin + Vector3.forward * (length * 0.5f - 2f) + Vector3.down * 0.5f, new Vector3(Width + 1f, 0.5f, length + 4f), new Color(0.85f, 0.8f, 0.98f));
        foreach (float side in new[] { -1f, 1f })
            ArenaProps.Block(transform, PrimitiveType.Cube, Origin + Vector3.right * side * (Width * 0.5f + 0.5f) + Vector3.forward * (length * 0.5f - 2f),
                new Vector3(1f, WALL_HEIGHT, length + 4f), ArenaProps.Purple);
        ArenaProps.Block(transform, PrimitiveType.Cube, Origin + Vector3.forward * (FinishZ + 4f) + Vector3.down * 0.45f, new Vector3(Width, 0.5f, 7f), ArenaProps.Yellow);
        var stripe = ArenaProps.Block(transform, PrimitiveType.Cube, Origin + Vector3.forward * FinishZ, new Vector3(Width, 0.05f, 0.6f), Color.white);
        Destroy(stripe.GetComponent<Collider>());

        _doors = new Transform[Rows * DOORS];
        _doorColliders = new Collider[Rows * DOORS];
        _shown = new byte[Rows * DOORS];
        var frames = new[] { ArenaProps.Pink, ArenaProps.Blue, ArenaProps.Green };
        for (int row = 0; row < Rows; row++)
        {
            var z = Vector3.forward * RowZ(row);
            // Posts between doors and a lintel above them: the only way through is a door.
            for (int p = 0; p <= DOORS; p++)
                ArenaProps.Block(transform, PrimitiveType.Cube, Origin + z + Vector3.right * (p * DOOR_WIDTH - Width * 0.5f), new Vector3(0.35f, WALL_HEIGHT, 0.6f), frames[row % frames.Length]);
            ArenaProps.Block(transform, PrimitiveType.Cube, Origin + z + Vector3.up * DOOR_HEIGHT, new Vector3(Width, WALL_HEIGHT - DOOR_HEIGHT, 0.6f), frames[row % frames.Length]);
            for (int d = 0; d < DOORS; d++)
            {
                int i = row * DOORS + d;
                var door = ArenaProps.Block(transform, PrimitiveType.Cube, Origin + z + Vector3.right * ((d + 0.5f) * DOOR_WIDTH - Width * 0.5f),
                    new Vector3(DOOR_WIDTH - 0.4f, DOOR_HEIGHT, 0.3f), new Color(0.98f, 0.85f, 0.55f));
                _doors[i] = door;
                _doorColliders[i] = door.GetComponent<Collider>();
                _shown[i] = 255;
            }
        }
        ArenaProps.Done();
    }

    protected override void OnIntro()
    {
        base.OnIntro();
        // Real doors per row; at least one, never all.
        int real = Variant == 1 ? 2 : 3;
        for (int row = 0; row < Rows; row++)
        {
            var order = new List<int> { 0, 1, 2, 3, 4 };
            for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            for (int d = 0; d < DOORS; d++) DoorState.Set(row * DOORS + d, FAKE);
            for (int k = 0; k < real; k++) DoorState.Set(row * DOORS + order[k], REAL);
        }
    }

    protected override Vector3 StartPosition(int slot, int count, out float yaw)
    {
        yaw = 0f;
        float x = (slot - (count - 1) * 0.5f) * 2f;
        return Origin + new Vector3(x, 0.5f, START_LENGTH * 0.4f);
    }

    protected override float Progress(PlayerRef player, Vector3 position) => position.z - Origin.z;
    protected override bool IsFinished(float progress) => progress >= FinishZ;

    protected override Vector3 RespawnPoint(PlayerRef player, float bestProgress, out float yaw)
    {
        yaw = 0f;
        float z = START_LENGTH * 0.4f;
        for (int row = 0; row < Rows; row++)
            if (bestProgress > RowZ(row) + 1f) z = RowZ(row) + 2f;
        return Origin + new Vector3(Random.Range(-Width * 0.3f, Width * 0.3f), 0.5f, z);
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier != ChaosModifier.Twist) return;
        for (int i = 0; i < Rows * DOORS; i++)
            if (DoorState[i] == BROKEN) DoorState.Set(i, REAL);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive) return;
        // A real door bursts when someone runs into it.
        foreach (var entry in _playerSpawner.Players)
        {
            var player = entry.Value;
            if (player == null || player.IsSpectator) continue;
            var rel = player.transform.position - Origin;
            for (int row = 0; row < Rows; row++)
            {
                if (Mathf.Abs(rel.z - RowZ(row)) > 1f) continue;
                int d = Mathf.FloorToInt((rel.x + Width * 0.5f) / DOOR_WIDTH);
                if (d < 0 || d >= DOORS) continue;
                int i = row * DOORS + d;
                if (DoorState[i] == REAL)
                {
                    DoorState.Set(i, BROKEN);
                    RPC_Burst(_doors[i].position);
                }
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Burst(Vector3 position)
    {
        Sfx.Play(Sound.Crack, 0.9f, 0.15f);
        LowPolyExplosion.Play(position, 1.6f, Resources.Load<Material>("ThrowArc"));
    }

    public override void Render()
    {
        base.Render();
        if (_doors == null) return;
        for (int i = 0; i < _doors.Length; i++)
        {
            byte state = DoorState[i];
            if (state == _shown[i]) continue;
            _shown[i] = state;
            _doors[i].gameObject.SetActive(state != BROKEN);
            _doorColliders[i].enabled = state != BROKEN;
        }
    }

    // Host physics opens doors even when nothing renders.
    private void LateUpdate()
    {
        if (_doors == null || Object == null || !Object.IsValid || !HasStateAuthority) return;
        for (int i = 0; i < _doors.Length; i++)
            if (DoorState[i] == BROKEN && _doorColliders[i].enabled) { _doorColliders[i].enabled = false; _doors[i].gameObject.SetActive(false); }
    }
}
