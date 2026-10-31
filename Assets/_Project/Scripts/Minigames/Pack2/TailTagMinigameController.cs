using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Tail Tag (after Fall Guys): half the contestants start with a tail. Without one, catch a tail-wearer and press
/// E or click to grab it (a fresh owner can't lose it for a moment). Every second with a tail scores. At the bell,
/// tail-wearers place first (most tail time first), then everyone else by tail time. Layouts: Open Stage, Pillars,
/// Hedges. Twist: TAIL SHUFFLE, the tails jump to random contestants.
/// </summary>
public class TailTagMinigameController : ScoreMinigameController
{
    private const float ARENA_RADIUS = 10.5f, GRAB_RANGE = 3.1f, IMMUNE_SECONDS = 1.5f;

    /// <summary>Bit per player slot (PlayerId &amp; 31): wearing a tail.</summary>
    [Networked] private int Tails { get; set; }
    [Networked, Capacity(32)] private NetworkArray<int> ImmuneUntil { get; }

    public override string[] Variants => new[] { "Open Stage", "Pillars", "Hedges" };
    public override string TwistTitle => "TAIL SHUFFLE";
    public override string TwistDescription => "Every tail jumps to a random contestant.";
    protected override string ScoreUnit => "s with a tail";
    public override bool AllowShove => false;

    private readonly Dictionary<PlayerRef, Transform> _tailViews = new();

    private static int Bit(PlayerRef player) => 1 << (player.PlayerId & 31);
    public bool HasTail(PlayerRef player) => (Tails & Bit(player)) != 0;

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
        return ArenaCentre + Vector3.up * 0.5f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ARENA_RADIUS * 0.75f;
    }

    public override void Spawned()
    {
        base.Spawned();
        ArenaProps.Fence(transform, ArenaCentre, ARENA_RADIUS + 1.5f);
        if (Variant == 1)
            for (int i = 0; i < 6; i++)
            {
                float angle = i / 6f * Mathf.PI * 2f;
                ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cylinder, ArenaCentre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 5f,
                    new Vector3(1.8f, 2.8f, 1.8f), ArenaProps.Pink), "Tower", true);
            }
        else if (Variant == 2)
            for (int i = 0; i < 4; i++)
            {
                float angle = (i * 90f + 45f) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // A hedge: three bushes over one collision box.
                var hedge = ArenaProps.Block(transform, PrimitiveType.Cube, ArenaCentre + direction * 5f, new Vector3(5f, 1.4f, 1.2f), ArenaProps.Green,
                    Quaternion.LookRotation(direction).eulerAngles.y + 90f);
                hedge.GetComponent<Renderer>().enabled = false;
                for (int b = -1; b <= 1; b++)
                    ArenaModels.Spawn("Bush", transform, hedge.position + hedge.right * b * 1.7f, hedge.rotation * Quaternion.Euler(0f, b * 40f, 0f), new Vector3(2f, 1.6f, 1.6f));
            }
        ArenaProps.Done();
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        DealTails();
    }

    private void DealTails()
    {
        var players = ConnectedPlayers().OrderBy(_ => Random.value).ToList();
        // Fewer tails than hunters: 2-3 players share one tail, 4-5 two, 6 three.
        int tails = Mathf.Max(1, players.Count / 2);
        int mask = 0;
        for (int i = 0; i < tails && i < players.Count; i++) mask |= Bit(players[i]);
        Tails = mask;
    }

    protected override void OnChaosStarted(ChaosModifier modifier)
    {
        if (modifier == ChaosModifier.Twist) { DealTails(); Announce("TAILS SHUFFLED!"); }
    }

    public override bool HandlePrimaryAction(Player player) => TryGrab(player);
    public override bool HandleShove(Player player) => TryGrab(player);

    /// <summary>Host. Grab the nearest tail in front of you within reach.</summary>
    private bool TryGrab(Player player)
    {
        if (!IsGameActive) return false;
        var me = player.Object.InputAuthority;
        if (HasTail(me) || player.IsSpectator) return true;
        var look = player.Interaction != null ? player.Interaction.LookDirection : player.transform.forward;
        var flat = new Vector3(look.x, 0f, look.z).normalized;
        PlayerRef best = PlayerRef.None;
        float bestDistance = float.MaxValue;
        foreach (var entry in _playerSpawner.Players)
        {
            if (entry.Value == null || entry.Key == me || !HasTail(entry.Key)) continue;
            if (Runner.Tick < ImmuneUntil[entry.Key.PlayerId & 31]) continue;
            // Where the rival was on the grabber's screen (lag compensation), not where the host has them now.
            var to = Rewind.PositionSeenBy(player, entry.Value) - player.transform.position;
            to.y = 0f;
            if (to.magnitude > GRAB_RANGE || (to.magnitude > 1.4f && Vector3.Angle(flat, to) > 110f)) continue;
            if (to.magnitude < bestDistance) { bestDistance = to.magnitude; best = entry.Key; }
        }
        if (best == PlayerRef.None) return true;
        Tails = (Tails & ~Bit(best)) | Bit(me);
        ImmuneUntil.Set(me.PlayerId & 31, Runner.Tick + Mathf.CeilToInt(IMMUNE_SECONDS * Runner.TickRate));
        RPC_Grabbed(player.transform.position, me, best);
        return true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Grabbed(Vector3 position, PlayerRef taker, PlayerRef loser)
    {
        Sfx.Play(Sound.Pop, 1f, 0.1f);
        string text = taker == Runner.LocalPlayer ? "GOT A TAIL!" : loser == Runner.LocalPlayer ? "TAIL STOLEN!" : "GRAB!";
        WorldPopup.Show(position + Vector3.up * 2.6f, text, new Color(1f, 0.6f, 0.2f), 8f);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive || Runner.Tick % Runner.TickRate != 0) return;
        foreach (var player in ConnectedPlayers())
            if (HasTail(player)) AddScore(player, 1);
    }

    public override string Callout => IsGameActive && Runner.LocalPlayer.IsRealPlayer
        ? HasTail(Runner.LocalPlayer) ? "<color=#E8590C>YOU HAVE A TAIL - RUN!</color>" : "<color=#1971C2>GRAB A TAIL! (E)</color>"
        : null;

    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings()
    {
        var withTail = ConnectedPlayers().Where(HasTail).GroupBy(ScoreOf).OrderByDescending(g => g.Key);
        var without = ConnectedPlayers().Where(p => !HasTail(p)).GroupBy(ScoreOf).OrderByDescending(g => g.Key);
        return withTail.Concat(without).Select(g => (IReadOnlyList<PlayerRef>)g.ToList()).ToList();
    }

    public override void Render()
    {
        base.Render();
        foreach (var entry in _playerSpawner.Players)
        {
            if (entry.Value == null) continue;
            bool show = (IsGameActive || Phase == MinigamePhase.Finished) && HasTail(entry.Key) && !entry.Value.IsSpectator;
            if (!_tailViews.TryGetValue(entry.Key, out var tail) || tail == null)
            {
                if (!show) continue;
                _tailViews[entry.Key] = tail = BuildTail();
            }
            tail.gameObject.SetActive(show);
            if (!show) continue;
            var body = entry.Value.transform;
            float wag = Mathf.Sin(Time.time * 9f + entry.Key.PlayerId) * 18f;
            tail.SetPositionAndRotation(body.position + Vector3.up * 0.75f - body.forward * 0.25f, body.rotation * Quaternion.Euler(-35f, wag, 0f));
        }
    }

    private Transform BuildTail()
    {
        var root = new GameObject("Tail").transform;
        root.SetParent(transform, false);
        var block = new MaterialPropertyBlock();
        void Piece(Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = ArenaProps.Material;
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }
        Piece(new Vector3(0f, 0f, -0.35f), new Vector3(0.32f, 0.32f, 0.75f), new Color(1f, 0.5f, 0.12f));
        Piece(new Vector3(0f, 0f, -0.75f), new Vector3(0.36f, 0.36f, 0.45f), new Color(1f, 0.5f, 0.12f));
        Piece(new Vector3(0f, 0f, -1.02f), new Vector3(0.28f, 0.28f, 0.3f), Color.white);
        return root;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        foreach (var tail in _tailViews.Values) if (tail != null) Destroy(tail.gameObject);
    }
}
