using Fusion;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Prize Grab: prizes rain onto the studio stage. Grab one (E), carry it to your own podium and press E there, or
/// throw it in, to bank it. Carrying slows you down and a shove knocks the prize out of your hands, so the stage is
/// always busy. Gift boxes are worth 1, golden chests 3; the last seconds are a bonus rain of golden chests.
/// Most points when time runs out wins. Podiums sit on a ring around the stage, one per contestant.
/// </summary>
public class PrizeGrabMinigameController : MinigameController
{
    public const int MAX_PLAYERS = 8;

    [SerializeField] private NetworkPrefabRef _prizePrefab;
    [SerializeField, Tooltip("Centre of the stage")] private Transform _stageCentre;
    [SerializeField] private float _rainRadius = 11f;
    [SerializeField] private float _podiumRadius = 15f;
    [SerializeField] private float _bankRange = 2.4f;
    [SerializeField, Tooltip("Seconds between prizes with 2 players; more players get them faster")] private float _rainInterval = 1.6f;
    [SerializeField] private int _maxPrizesPerPlayer = 2;
    [SerializeField, Range(0f, 1f)] private float _goldenChance = 0.15f;
    [Header("Golden Rain (once, mid-round) and the Prize Storm twist")]
    [SerializeField, Tooltip("How long the mid-round Golden Rain lasts")] private float _goldenRainSeconds = 4f;
    [SerializeField, Range(0f, 1f), Tooltip("Share of Golden Rain drops that are golden chests (the rest are gift boxes)")]
    private float _goldenRainShare = 0.5f;
    [SerializeField, Tooltip("Drop rate during Golden Rain, times the normal rate")] private float _goldenRainRate = 1.4f;
    [SerializeField, Range(0f, 1f), Tooltip("Share of Prize Storm drops that are golden chests")] private float _stormGoldenShare = 0.4f;
    [SerializeField, Tooltip("Drop rate during Prize Storm, times the normal rate")] private float _stormRate = 2f;
    [SerializeField, Tooltip("Prizes allowed on stage during Prize Storm, times the normal cap")] private float _stormCap = 1.5f;

    [Networked, Capacity(MAX_PLAYERS)] private NetworkDictionary<PlayerRef, int> Scores => default;
    [Networked] private TickTimer RainTimer { get; set; }
    [Networked] private NetworkBool BonusAnnounced { get; set; }
    [Networked] private float GoldenAt { get; set; }

    private readonly List<PlayerRef> _order = new();
    private readonly Dictionary<PlayerRef, Transform> _podiums = new();
    private readonly Dictionary<PlayerRef, string> _shownNames = new();
    private Transform _podiumRoot;

    public int ScoreOf(PlayerRef player) => Scores.TryGet(player, out var s) ? s : 0;

    public override string TwistTitle => "PRIZE STORM";
    public override string TwistKind => "BUFF";
    public override string TwistDescription => "Golden prizes pour down from the sky.";

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        int mine = ScoreOf(Runner.LocalPlayer);
        int best = 0;
        foreach (var pair in Scores) best = Mathf.Max(best, pair.Value);
        return $"You banked <b>{mine}</b>   <size=80%>best on stage: {best}</size>";
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        foreach (var p in ConnectedPlayers())
            Scores.Set(p, 0);
        RainTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
    }

    /// <summary>Podium position for a contestant: evenly spaced by join order, so every peer agrees.</summary>
    public Vector3 PodiumPosition(PlayerRef player)
    {
        var players = SortedPlayers();
        return PodiumPosition(players.IndexOf(player), players.Count);
    }

    private Vector3 PodiumPosition(int slot, int count)
    {
        count = Mathf.Max(1, count);
        float angle = (slot + 0.5f) / count * Mathf.PI * 2f;
        return _stageCentre.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _podiumRadius;
    }

    private List<PlayerRef> SortedPlayers()
    {
        _order.Clear();
        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null) _order.Add(entry.Key);
        _order.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return _order;
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive)
            return;

        // One short Golden Rain at a random moment mid-round (at the very end it decided every match).
        float total = Definition.GameDuration * MatchSettings.LengthMultiplier;
        float elapsed = total - TimeRemaining;
        if (GoldenAt <= 0f) GoldenAt = total * Random.Range(0.35f, 0.65f);
        bool bonus = IsTimeLimited && elapsed >= GoldenAt && elapsed < GoldenAt + _goldenRainSeconds;
        if (bonus && !BonusAnnounced)
        {
            BonusAnnounced = true;
            Announce("GOLDEN RAIN!");
        }

        int players = Mathf.Max(2, ConnectedPlayers().Count());
        if (RainTimer.ExpiredOrNotRunning(Runner))
        {
            // Golden Rain and Prize Storm speed the drops up a little and make some of them golden, not all: a burst
            // of all-golden chests decided whole matches.
            float interval = _rainInterval * 2f / players / (bonus ? _goldenRainRate : 1f) / (TwistActive ? _stormRate : 1f);
            RainTimer = TickTimer.CreateFromSeconds(Runner, interval);
            float goldenChance = TwistActive ? _stormGoldenShare : bonus ? _goldenRainShare : _goldenChance;
            if (Prize.Active.Count < players * _maxPrizesPerPlayer * (TwistActive ? _stormCap : 1f))
                DropPrize(Random.value < goldenChance);
        }

        // Prizes thrown or dropped onto a podium bank for that podium's owner.
        if (Runner.Tick % 6 == 0)
            for (int p = Prize.Active.Count - 1; p >= 0; p--)
            {
                var prize = Prize.Active[p];
                if (prize.Object == null || !prize.Object.IsValid || prize.Grabbable.IsGrabbed) continue;
            {
                var owners = SortedPlayers();
                for (int i = 0; i < owners.Count; i++)
                {
                    var flat = prize.transform.position - PodiumPosition(i, owners.Count);
                    if (Mathf.Abs(flat.y) < 2.5f && new Vector2(flat.x, flat.z).magnitude < _bankRange * 0.8f)
                    {
                        Bank(owners[i], prize);
                        break;
                    }
                }
            }
            }

        // Prizes that fall off the stage come back down from the sky.
        if (Runner.Tick % 30 == 0)
            for (int p = Prize.Active.Count - 1; p >= 0; p--)
                if (Prize.Active[p] is var prize && !prize.Grabbable.IsGrabbed
                    && (prize.transform.position.y < _stageCentre.position.y - 6f || OutsideFence(prize.transform.position)))
                    Runner.Despawn(prize.Object);
    }

    /// <summary>Past the fence and down near the floor: out of everyone's reach, so it is gone.</summary>
    private bool OutsideFence(Vector3 position) =>
        Vector3.ProjectOnPlane(position - _stageCentre.position, Vector3.up).magnitude > _podiumRadius + 2.5f
        && position.y < _stageCentre.position.y + 1f;

    private int _dropTurn;

    /// <summary>Prizes take turns falling toward each contestant's side of the stage, so nobody's half stays empty.</summary>
    private void DropPrize(bool golden)
    {
        var players = ConnectedPlayers().OrderBy(p => p.PlayerId).ToList();
        var flat = Random.insideUnitCircle * _rainRadius;
        if (players.Count > 0)
        {
            var towards = PodiumPosition(_dropTurn++ % players.Count, players.Count) - _stageCentre.position;
            var side = new Vector2(towards.x, towards.z).normalized * Random.Range(_rainRadius * 0.35f, _rainRadius * 0.85f);
            flat = side + Random.insideUnitCircle * 2.5f;
        }
        var position = _stageCentre.position + new Vector3(flat.x, 12f, flat.y);
        Runner.Spawn(_prizePrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), null,
            (runner, spawned) => spawned.GetComponent<Prize>().Value = (byte)(golden ? 3 : 1));
    }

    public override string[] Variants => new[] { "Open Stage", "Hurdles", "Towers" };

    public override void Spawned()
    {
        base.Spawned();
        var ground = _stageCentre.position;
        // Contestants stay inside the podium ring; prizes thrown wide sail over the fence and are lost.
        ArenaProps.Fence(transform, ground, _podiumRadius + 2.5f);
        if (Variant == 1)
        {
            // A broken ring of low hurdles: hop them with a prize, or go round through the gaps.
            for (int i = 0; i < 6; i++)
            {
                float angle = (i * 60f + 15f) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cube, ground + direction * 7f, new Vector3(4f, 0.9f, 0.4f), ArenaProps.Yellow,
                    Quaternion.LookRotation(direction).eulerAngles.y), "WoodFence");
            }
        }
        else if (Variant == 2)
        {
            // Low towers to jump on: prizes landing up there are a scramble.
            for (int i = 0; i < 3; i++)
            {
                float angle = (i * 120f + 30f) * Mathf.Deg2Rad;
                ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cube, ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 5.5f,
                    new Vector3(3.2f, 1.1f, 3.2f), ArenaProps.Pink, i * 40f), i == 0 ? "BrickTall" : i == 1 ? "Brick4" : "Brick6");
            }
            ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cube, ground, new Vector3(2.4f, 1.6f, 2.4f), ArenaProps.Purple), "Crate");
        }
        ArenaProps.Done();
    }

    public override bool HandlePrimaryAction(Player player)
    {
        if (!IsGameActive) return false;
        var held = player.Interaction != null ? player.Interaction.HeldObject : null;
        if (held == null || !held.TryGetComponent<Prize>(out var prize)) return false;

        var who = player.Object.InputAuthority;
        var to = PodiumPosition(who) - player.transform.position;
        to.y = 0f;
        if (to.magnitude > _bankRange) return false;   // not at your podium: E drops it as usual

        Bank(who, prize);
        return true;
    }

    private void Bank(PlayerRef player, Prize prize)
    {
        int value = prize.Value;
        Scores.Set(player, ScoreOf(player) + value);
        Runner.Despawn(prize.Object);
        RPC_Banked(player, value);
    }

    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings()
    {
        // Highest score first; equal scores tie.
        return ConnectedPlayers()
            .GroupBy(ScoreOf)
            .OrderByDescending(g => g.Key)
            .Select(g => (IReadOnlyList<PlayerRef>)g.ToList())
            .ToList();
    }

    public override void HandlePlayerLeft(PlayerRef player)
    {
        if (IsGameActive && ConnectedPlayers().Count() <= 1)
            FinishRound(GetTimeUpStandings());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Banked(PlayerRef player, int value)
    {
        Sfx.Play(value > 1 ? Sound.Fanfare : Sound.Safe, 0.8f, 0.1f);
        WorldPopup.Show(PodiumPosition(player) + Vector3.up * 3f, $"+{value}", value > 1 ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 1f, 0.6f), value > 1 ? 9f : 7f);
    }

    #region Podium visuals (local)

    public override void Render()
    {
        base.Render();
        if (_stageCentre == null) return;
        if (_podiumRoot == null)
            _podiumRoot = new GameObject("Podiums").transform;

        var players = SortedPlayers();
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (!_podiums.TryGetValue(player, out var podium) || podium == null)
                _podiums[player] = podium = BuildPodium(player);
            podium.position = PodiumPosition(i, players.Count);
            RefreshPodium(player, podium);
        }
    }

    /// <summary>
    /// Names and colours replicate after the podiums are first built, so keep them current; your own podium gets a
    /// bouncing "YOU" marker so nobody has to guess which one is theirs.
    /// </summary>
    private void RefreshPodium(PlayerRef player, Transform podium)
    {
        if (!_playerSpawner.Players.TryGet(player, out var p) || p == null) return;
        var label = podium.Find("Name")?.GetComponent<TextMeshPro>();
        bool mine = player == Runner.LocalPlayer;
        if (label != null && mine)
            label.transform.localPosition = new Vector3(0f, 3f + Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 0.5f, 0f);
        if (_shownNames.TryGetValue(player, out var shown) && shown == p.Nickname) return;
        _shownNames[player] = p.Nickname;
        string name = string.IsNullOrEmpty(p.Nickname) ? $"Player {player.PlayerId}" : p.Nickname;
        string text = mine ? $"<size=150%><color=#FFCA3D>YOU</color></size>\n{name}" : name;
        if (label != null && label.text != text)
        {
            label.text = text;
            label.color = p.PlayerColor;
            var pad = podium.Find("Pad")?.GetComponent<Renderer>();
            if (pad != null) pad.material.color = new Color(p.PlayerColor.r, p.PlayerColor.g, p.PlayerColor.b, 0.85f);
        }
    }

    private Transform BuildPodium(PlayerRef player)
    {
        var root = new GameObject("Podium " + player.PlayerId).transform;
        root.SetParent(_podiumRoot, false);
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "Pad";
        Destroy(pad.GetComponent<Collider>());
        pad.transform.SetParent(root, false);
        pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pad.transform.localScale = new Vector3(_bankRange * 1.6f, 0.08f, _bankRange * 1.6f);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(root, false);
        ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        ring.transform.localScale = new Vector3(_bankRange * 1.9f, 0.05f, _bankRange * 1.9f);

        Color color = Color.white;
        string name = "";
        if (_playerSpawner.Players.TryGet(player, out var p) && p != null)
        {
            color = p.PlayerColor;
            name = p.Nickname;
        }
        var mat = Resources.Load<Material>("ThrowArc");
        var padMat = new Material(mat) { color = new Color(color.r, color.g, color.b, 0.85f) };
        pad.GetComponent<Renderer>().material = padMat;
        ring.GetComponent<Renderer>().material = new Material(mat) { color = new Color(1f, 1f, 1f, 0.9f) };

        var label = new GameObject("Name");
        label.transform.SetParent(root, false);
        label.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        var text = label.AddComponent<TextMeshPro>();
        text.text = name;
        text.fontSize = 5f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(46, 26, 71, 255);
        label.AddComponent<Billboard>();
        return root;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (_podiumRoot != null) Destroy(_podiumRoot.gameObject);
    }

    #endregion
}
