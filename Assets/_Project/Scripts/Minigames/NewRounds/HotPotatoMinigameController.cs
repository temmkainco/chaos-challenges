using DG.Tweening;
using Fusion;
using System.Linq;
using UnityEngine;

/// <summary>
/// Hot Potato: one contestant carries a ticking bomb over their head. Touch a rival to pass it on (it cannot come
/// straight back). When the fuse runs out the holder is blasted out and a new bomb goes to someone else; the fuse
/// gets shorter as fewer remain. Last contestant standing wins. The holder runs a little faster.
/// Chaos: SLOW MOTION and ICE FLOOR make passing harder; its own twist, BOMB HOP, moves the bomb on its own.
/// </summary>
public class HotPotatoMinigameController : EliminationMinigameController
{
    [SerializeField] private Transform _centre;
    [SerializeField, Tooltip("Bumping into someone (centre to centre) passes the bomb")] private float _passRange = 1.4f;
    [SerializeField, Tooltip("Click (or E) hands the bomb to the rival in front within this reach")] private float _handReach = 3.2f;
    [SerializeField] private Vector2 _fuseSeconds = new(9f, 15f);

    [Networked] public PlayerRef Holder { get; private set; }
    [Networked] private TickTimer Fuse { get; set; }
    [Networked] private TickTimer PassCooldown { get; set; }
    [Networked] private PlayerRef PreviousHolder { get; set; }
    [Networked] private TickTimer NoPassBack { get; set; }
    [Networked] private TickTimer NextHop { get; set; }

    public override string TwistTitle => "BOMB HOP";
    public override string TwistDescription => "Every few seconds the bomb jumps to a random contestant.";

    private Transform _bomb;
    private float _blink;

    public override string[] Variants => new[] { "Open Stage", "Pillars", "Crossroads" };

    public override void Spawned()
    {
        base.Spawned();
        var ground = _centre.position;
        if (Variant == 1)
        {
            // A ring of pillars to dodge around (and lose your pursuer behind).
            for (int i = 0; i < 6; i++)
            {
                float angle = i / 6f * Mathf.PI * 2f;
                ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cylinder, ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 4.5f,
                    new Vector3(1.8f, 2.8f, 1.8f), ArenaProps.Pink), "Tower", true);
            }
            ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cylinder, ground, new Vector3(2.4f, 3.6f, 2.4f), ArenaProps.Yellow), "Tower", true);
        }
        else if (Variant == 2)
        {
            // Four low walls in an X: hop over them or run around.
            for (int i = 0; i < 4; i++)
            {
                float angle = (i * 90f + 45f) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                ArenaProps.Skin(ArenaProps.Block(transform, PrimitiveType.Cube, ground + direction * 4f, new Vector3(0.8f, 1.1f, 4.5f), ArenaProps.Purple,
                    Quaternion.LookRotation(direction).eulerAngles.y), "WoodFence", yawOffset: 90f);
            }
        }
        ArenaProps.Done();
    }

    // Just below the stage top: stepping down onto the studio floor counts as out (no walking back in).
    protected override float OutHeight => _centre.position.y - 0.8f;

    protected override Vector3 ArenaSpawn(int slot, int count)
    {
        float angle = (slot + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
        return _centre.position + Vector3.up * 0.5f + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 8f;
    }

    public override string GetStatusText()
    {
        if (!IsGameActive || Holder == PlayerRef.None) return null;
        return Holder == Runner.LocalPlayer
            ? "<color=#E03131>YOU have the bomb - click a rival to hand it over!</color>"
            : $"<b>{NameOf(Holder)}</b> has the bomb - run!";
    }

    protected override void StartGame()
    {
        base.StartGame();
        _lastBlast = null;
        if (HasStateAuthority) NewBomb();
    }

    /// <summary>Where the last bomb went off: the next one goes to whoever is nearest (not someone a field away).</summary>
    private Vector3? _lastBlast;

    private void NewBomb()
    {
        if (_alive.Count <= 1) return;
        Holder = _alive[Random.Range(0, _alive.Count)];
        if (_lastBlast.HasValue)
        {
            float best = float.MaxValue;
            foreach (var who in _alive)
                if (_playerSpawner.Players.TryGet(who, out var p) && p != null && (p.transform.position - _lastBlast.Value).sqrMagnitude < best)
                {
                    best = (p.transform.position - _lastBlast.Value).sqrMagnitude;
                    Holder = who;
                }
        }
        PreviousHolder = PlayerRef.None;
        float scale = Mathf.Lerp(0.6f, 1f, (_alive.Count - 2) / 4f);
        Fuse = TickTimer.CreateFromSeconds(Runner, Random.Range(_fuseSeconds.x, _fuseSeconds.y) * scale);
        PassCooldown = TickTimer.CreateFromSeconds(Runner, 1f);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority || !IsGameActive || Holder == PlayerRef.None) return;
        if (!_playerSpawner.Players.TryGet(Holder, out var holder) || holder == null) { NewBomb(); return; }

        if (Fuse.Expired(Runner))
        {
            var blasted = Holder;
            Holder = PlayerRef.None;
            if (holder.TryGetComponent<PlayerMovement>(out var movement))
                movement.Launch(Random.insideUnitSphere.WithY(0f).normalized * 6f + Vector3.up * 12f);
            RPC_Boom(holder.transform.position);
            _lastBlast = holder.transform.position;
            Eliminate(blasted);
            if (IsGameActive) NewBomb();
            return;
        }

        // Bomb Hop: the bomb jumps to a random rival every 3 seconds.
        if (TwistActive && _alive.Count > 1 && NextHop.ExpiredOrNotRunning(Runner))
        {
            if (NextHop.IsRunning)
            {
                var others = _alive.Where(p => p != Holder).ToList();
                var next = others[Random.Range(0, others.Count)];
                if (_playerSpawner.Players.TryGet(next, out var hopTo) && hopTo != null)
                {
                    PreviousHolder = PlayerRef.None;
                    Holder = next;
                    PassCooldown = TickTimer.CreateFromSeconds(Runner, 0.6f);
                    RPC_Pass(hopTo.transform.position, next, true);
                }
            }
            NextHop = TickTimer.CreateFromSeconds(Runner, 3f);
            return;
        }
        if (!TwistActive) NextHop = TickTimer.None;

        if (PreviousHolder != PlayerRef.None && NoPassBack.ExpiredOrNotRunning(Runner))
            PreviousHolder = PlayerRef.None;
        if (!PassCooldown.ExpiredOrNotRunning(Runner)) return;
        foreach (var other in _alive)
        {
            if (other == Holder || other == PreviousHolder) continue;
            if (!_playerSpawner.Players.TryGet(other, out var target) || target == null) continue;
            if (Vector3.Distance(target.transform.position, holder.transform.position) > _passRange) continue;
            PreviousHolder = Holder;
            Holder = other;
            PassCooldown = TickTimer.CreateFromSeconds(Runner, 0.6f);
            NoPassBack = TickTimer.CreateFromSeconds(Runner, 1.5f);
            RPC_Pass(target.transform.position, other, false);
            break;
        }
    }

    /// <summary>The holder's click hands the bomb to the rival right in front (deliberate, unlike bumping).</summary>
    public override bool HandleShove(Player player) => TryHandOver(player);
    public override bool HandlePrimaryAction(Player player) => TryHandOver(player);

    private bool TryHandOver(Player player)
    {
        if (!IsGameActive || player.Object.InputAuthority != Holder) return false;
        if (!PassCooldown.ExpiredOrNotRunning(Runner)) return true;
        var look = player.Interaction != null ? player.Interaction.LookDirection : player.transform.forward;
        var flat = new Vector3(look.x, 0f, look.z);
        flat = flat.sqrMagnitude > 0.001f ? flat.normalized : player.transform.forward;
        PlayerRef best = PlayerRef.None;
        Player bestPlayer = null;
        float bestScore = float.MaxValue;
        foreach (var other in _alive)
        {
            if (other == Holder || !_playerSpawner.Players.TryGet(other, out var target) || target == null) continue;
            var to = (target.transform.position - player.transform.position).WithY(0f);
            float angle = Vector3.Angle(flat, to);
            if (to.magnitude > _handReach || angle > 60f) continue;
            float score = to.magnitude + angle / 30f;
            if (score < bestScore) { bestScore = score; best = other; bestPlayer = target; }
        }
        if (bestPlayer == null) return true;   // nobody in reach: the click does nothing (no shove either)
        PreviousHolder = Holder;
        Holder = best;
        PassCooldown = TickTimer.CreateFromSeconds(Runner, 0.6f);
        NoPassBack = TickTimer.CreateFromSeconds(Runner, 1.5f);
        RPC_Pass(bestPlayer.transform.position, best, false);
        return true;
    }

    protected override void OnEliminated(PlayerRef player)
    {
        if (player == Holder) { Holder = PlayerRef.None; NewBomb(); }
    }

    public override void Render()
    {
        base.Render();
        if (_bomb == null)
        {
            _bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Destroy(_bomb.GetComponent<Collider>());
            _bomb.name = "HotPotato";
            _bomb.localScale = Vector3.one * 0.9f;
            var fuse = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            Destroy(fuse.GetComponent<Collider>());
            fuse.SetParent(_bomb, false);
            fuse.localPosition = new Vector3(0f, 0.6f, 0f);
            fuse.localScale = new Vector3(0.18f, 0.2f, 0.18f);
            var mat = Resources.Load<Material>("ThrowArc");
            _bomb.GetComponent<Renderer>().material = new Material(mat) { color = new Color(0.16f, 0.15f, 0.2f) };
            fuse.GetComponent<Renderer>().material = new Material(mat) { color = new Color(1f, 0.6f, 0.2f) };
        }
        bool show = IsGameActive && Holder != PlayerRef.None && _playerSpawner.Players.TryGet(Holder, out var holder) && holder != null;
        _bomb.gameObject.SetActive(show);
        if (!show) return;
        _playerSpawner.Players.TryGet(Holder, out var h);
        _bomb.position = Vector3.Lerp(_bomb.position, h.transform.position + Vector3.up * 2.7f, 1f - Mathf.Exp(-20f * Time.deltaTime));
        // Pulse faster as the fuse burns down (players never see the exact time).
        float left = Fuse.RemainingTime(Runner) ?? 0f;
        _blink += Time.deltaTime * Mathf.Lerp(14f, 3f, Mathf.Clamp01(left / 8f));
        _bomb.localScale = Vector3.one * (0.9f + 0.08f * Mathf.Sin(_blink));
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (_bomb != null) Destroy(_bomb.gameObject);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Pass(Vector3 position, PlayerRef to, NetworkBool hop)
    {
        Sfx.Play(Sound.Pop, 1f, 0.1f);
        string text = to == Runner.LocalPlayer ? (hop ? "HOP! YOURS!" : "TAG! YOURS!") : $"{(hop ? "HOP" : "TAG")}: {NameOf(to)}";
        WorldPopup.Show(position + Vector3.up * 3.2f, text, to == Runner.LocalPlayer ? new Color(1f, 0.37f, 0.36f) : new Color(1f, 0.79f, 0.24f), 8f);
    }

    private string NameOf(PlayerRef player) =>
        _playerSpawner.Players.TryGet(player, out var p) && p != null && !string.IsNullOrEmpty(p.Nickname) ? p.Nickname : $"Player {player.PlayerId}";

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Boom(Vector3 position)
    {
        Sfx.Play(Sound.Boom, 1f, 0.1f);
        LowPolyExplosion.Play(position + Vector3.up * 1.5f, 2.6f, Resources.Load<Material>("ThrowArc"));
    }
}
