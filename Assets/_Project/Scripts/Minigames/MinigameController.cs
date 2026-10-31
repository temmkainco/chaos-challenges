using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

public enum MinigamePhase : byte
{
    Waiting,
    Countdown,
    Playing,
    Finished,
    /// <summary>Rules card and arena fly-over; everyone readies up (or the timer runs out), then the countdown.</summary>
    Intro,
}

public enum VehicleKind : byte { None, Kart, Plane }

/// <summary>
/// Base for every minigame. The host drives a networked <see cref="Phase"/> with one <see cref="PhaseTimer"/>
/// (start countdown, then the optional time limit); every peer raises the UI events from the replicated state.
/// Subclasses end the round with <see cref="FinishRound(IReadOnlyList{PlayerRef})"/> and can override
/// <see cref="HandlePlayerLeft"/> and <see cref="GetTimeUpStandings"/>.
/// </summary>
public class MinigameController : NetworkBehaviour
{
    /// <summary>Seconds before the time limit runs out from which the countdown UI shows the remaining time.</summary>
    public const int FINAL_SECONDS = 10;

    [Networked, OnChangedRender(nameof(OnPhaseChanged))] public MinigamePhase Phase { get; private set; }
    [Networked] public TickTimer PhaseTimer { get; private set; }
    /// <summary>The announcer's latest one-liner ("DRAW!"), shown as a big banner; the id bumps on every call.</summary>
    [Networked] public NetworkString<_32> Announcement { get; private set; }
    [Networked] public int AnnouncementId { get; private set; }
    /// <summary>Bit per <see cref="PlayerRef.PlayerId"/>: ready on the rules card.</summary>
    [Networked] public int ReadyMask { get; private set; }

    public const float INTRO_SECONDS = 15f;

    /// <summary>The chaos modifier in effect (see <see cref="ChaosDirector"/>); every peer applies it from this state.</summary>
    [Networked] public ChaosModifier ActiveChaos { get; private set; }
    [Networked] private TickTimer ChaosTimer { get; set; }
    public float ChaosTimeRemaining => ChaosTimer.RemainingTime(Runner) ?? 0f;
    [Networked] private TickTimer NextRandomChaos { get; set; }
    [Networked] private TickTimer NextMeteor { get; set; }
    /// <summary>Chaos about to start: announced <see cref="CHAOS_WARNING"/> seconds ahead so players can react.</summary>
    [Networked] public ChaosModifier PendingChaos { get; private set; }
    [Networked] private TickTimer PendingTimer { get; set; }
    [Networked] private float PendingSeconds { get; set; }
    public float PendingTimeRemaining => PendingTimer.RemainingTime(Runner) ?? 0f;
    public const float CHAOS_WARNING = 3f;

    /// <summary>The minigame of the loaded scene, on every peer.</summary>
    public static MinigameController Current { get; private set; }

    public bool IsGameActive => Phase == MinigamePhase.Playing;
    /// <summary>Rules, timings and texts of the round (Console Round swaps in the cartridge being played).</summary>
    public virtual MinigameDefinitionSO Definition => _minigameDefinition;
    public bool IsTimeLimited => Definition.IsTimeLimited && Definition.GameDuration > 0f;
    public float TimeRemaining => PhaseTimer.RemainingTime(Runner) ?? 0f;

    protected NetworkGameManager GameManager => NetworkGameManager.Instance;

    [SerializeField] protected MinigameDefinitionSO _minigameDefinition;
    [SerializeField] protected bool _hasCountdown = true;

    [Inject] protected BasePlayerSpawner _playerSpawner;

    public event Action OnMinigameStart;
    public event Action OnMinigameEnd;
    public event Action OnCountdownStarted;
    public event Action<int> OnCountdownTick;
    /// <summary>The last <see cref="FINAL_SECONDS"/> of a time-limited round, once per second.</summary>
    public event Action<int> OnTimeLimitTick;

    private int _lastShownSecond = -1;

    /// <summary>Host only: show a big announcer banner on every screen.</summary>
    protected void Announce(string text)
    {
        if (!HasStateAuthority) return;
        Announcement = text;
        AnnouncementId++;
    }

    /// <summary>Called by <see cref="NetworkGameManager"/> on the host once every player has loaded the scene.</summary>
    public void InitializeMinigame()
    {
        if (!HasStateAuthority || Phase != MinigamePhase.Waiting)
            return;

        SetPlayersInput(false);
        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null) entry.Value.IsSpectator = false;
        ReadyMask = 0;
        SetPhase(MinigamePhase.Intro, INTRO_SECONDS);
        OnIntro();
    }

    /// <summary>Host only. The rules card is up and players are frozen: place them on the arena here.</summary>
    protected virtual void OnIntro() { }

    private void BeginCountdown()
    {
        if (_hasCountdown && Definition.StartDelay > 0f)
        {
            SetPhase(MinigamePhase.Countdown, Definition.StartDelay);
            return;
        }
        StartGame();
    }

    public bool IsReady(PlayerRef player) => (ReadyMask & (1 << player.PlayerId)) != 0;
    public int ReadyCount => ConnectedPlayers().Count(IsReady);
    public int PlayerCount => ConnectedPlayers().Count();

    /// <summary>Sent by each player's rules card (controls are locked during the intro, so this is not gameplay input).</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Ready(RpcInfo info = default)
    {
        if (Phase != MinigamePhase.Intro) return;
        var player = info.Source == PlayerRef.None ? Runner.LocalPlayer : info.Source;
        ReadyMask |= 1 << player.PlayerId;
        if (ConnectedPlayers().All(IsReady))
            BeginCountdown();
    }

    public override void Spawned()
    {
        Current = this;
        var stage = GameObject.Find("StageCentre") ?? GameObject.Find("Stage");
        ArenaTheme.Apply(Theme, stage != null ? stage.transform.position : transform.position, transform);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>Host only. A player disconnected during this round.</summary>
    public virtual void HandlePlayerLeft(PlayerRef player) { }

    /// <summary>
    /// Host only. The player pressed the interact button. Return true when the minigame used it
    /// (e.g. firing the revolver), so the normal interaction does not run.
    /// </summary>
    public virtual bool HandlePrimaryAction(Player player) => false;

    /// <summary>Host only. The empty-handed throw button was pressed; return true to use it instead of a shove.</summary>
    public virtual bool HandleShove(Player player) => false;

    /// <summary>Whether contestants can shove each other (empty hands + throw button) in this minigame.</summary>
    public virtual bool AllowShove => MatchSettings.Shoving;
    /// <summary>Whether a shove makes the target drop what they hold (off in Key Hunt: your key stays yours).</summary>
    public virtual bool ShoveDropsHeldItem => true;
    /// <summary>Multiplier on shove knockback (Ring Out makes shoves the main weapon).</summary>
    public virtual float ShoveStrength => 1f;

    /// <summary>Side scoreboard entry for a contestant ("12", "Lap 2", "#1"); null for everyone hides the board.</summary>
    public virtual string ScoreboardValue(PlayerRef player) => null;
    /// <summary>Order on the side scoreboard, highest first.</summary>
    public virtual float ScoreboardRank(PlayerRef player) => 0f;

    /// <summary>Optional big, persistent instruction at the top of the screen while it matters (Color Code's colour).</summary>
    public virtual string Callout => null;

    /// <summary>Optional status line for the arena display (e.g. mines left, contestants left).</summary>
    public virtual string GetStatusText() => null;

    /// <summary>Whether the rules card lists the usual WASD / SPACE controls (off for Console Round's gamepads).</summary>
    public virtual bool UsesDefaultControls => true;

    /// <summary>Optional intro camera instead of the orbit (e.g. a dolly along an indoor set).</summary>
    public virtual bool TryGetIntroShot(float time, out Vector3 position, out Vector3 lookAt)
    {
        position = lookAt = default;
        return false;
    }

    /// <summary>
    /// A player touched the kill zone (called on every peer; only the host acts). Return true when the minigame
    /// handles it, false to send the player back to their spawn point.
    /// </summary>
    public virtual bool HandlePlayerFell(Player player) => false;

    /// <summary>What contestants ride in this minigame (karts, planes); <see cref="VehicleKind.None"/> = on foot.</summary>
    public virtual VehicleKind Vehicle => VehicleKind.None;

    /// <summary>Planes can't climb above this height.</summary>
    public virtual float MaxAltitude => float.MaxValue;

    /// <summary>
    /// Sideways push on contestants standing at <paramref name="position"/> (a rolling log carries you along). Must
    /// depend only on networked state, so prediction and the host agree.
    /// </summary>
    public virtual Vector3 GroundDrift(Vector3 position) => Vector3.zero;

    /// <summary>Whether Shift sprint works in this minigame (off where it skips the challenge, e.g. jumping Hex Drop holes).</summary>
    public virtual bool AllowsSprint => true;

    #region Arena layouts

    /// <summary>Layouts this minigame's arena can be built in (names for the rules card); null = just one.</summary>
    public virtual string[] Variants => null;

    /// <summary>This round's layout, from the host's <see cref="NetworkGameManager.RoundSeed"/> (same on every peer).</summary>
    public int Variant => VariantOf(Variants?.Length ?? 0);

    /// <summary>This round's layout out of <paramref name="count"/> (for arena parts that are not the controller).</summary>
    public static int VariantOf(int count)
    {
        if (count <= 1) return 0;
        var gameManager = NetworkGameManager.Instance;
        int seed = gameManager != null && gameManager.Object != null && gameManager.Object.IsValid ? gameManager.RoundSeed : 0;
        return (seed & int.MaxValue) % count;
    }

    public string VariantName => Variants is { Length: > 1 } variants ? variants[Variant] : null;

    /// <summary>
    /// Where this round's arena is set (<see cref="ArenaTheme"/>): by default the three layouts are the studio, a
    /// tropical ocean and outer space.
    /// </summary>
    public virtual ArenaThemeKind Theme => Variants is { Length: >= 3 } ? (ArenaThemeKind)(Variant % 3) : ArenaThemeKind.Studio;

    #endregion

    #region Chaos (buffs, debuffs, hazards for everyone; the future audience vote calls ChaosDirector.Request)

    /// <summary>Whether this minigame allows <paramref name="modifier"/> (override to veto ones that break the game).</summary>
    public virtual bool SupportsChaos(ChaosModifier modifier) =>
        modifier != ChaosModifier.None && (modifier != ChaosModifier.Twist || TwistTitle != null);

    /// <summary>This minigame's own chaos (<see cref="ChaosModifier.Twist"/>), e.g. "BROADSIDE"; null when it has none.</summary>
    public virtual string TwistTitle => null;
    /// <summary>One line for the rules card and the future vote page.</summary>
    public virtual string TwistDescription => null;
    /// <summary>"BUFF", "DEBUFF" or "HAZARD".</summary>
    public virtual string TwistKind => "HAZARD";
    public bool TwistActive => ActiveChaos == ChaosModifier.Twist;

    /// <summary>Extra push on every contestant (Tilefall's crosswind). Must be a pure function of networked state.</summary>
    public virtual Vector3 ChaosWind => Vector3.zero;

    /// <summary>Host only. Game-specific twist when a modifier starts (more bombs, faster cracks...).</summary>
    protected virtual void OnChaosStarted(ChaosModifier modifier) { }
    protected virtual void OnChaosEnded(ChaosModifier modifier) { }

    /// <summary>
    /// Host only; see <see cref="ChaosDirector.Request"/>. The modifier is announced first and starts
    /// <see cref="CHAOS_WARNING"/> seconds later (a new request replaces one still waiting).
    /// </summary>
    public bool StartChaos(ChaosModifier modifier, float seconds)
    {
        if (!HasStateAuthority || !IsGameActive || !SupportsChaos(modifier))
            return false;
        PendingChaos = modifier;
        PendingSeconds = seconds;
        PendingTimer = TickTimer.CreateFromSeconds(Runner, CHAOS_WARNING);
        Announce($"{ChaosInfo.Title(modifier)} IN 3...");
        return true;
    }

    private void ActivateChaos(ChaosModifier modifier, float seconds)
    {
        if (ActiveChaos != ChaosModifier.None)
            EndChaos();
        ActiveChaos = modifier;
        ChaosTimer = TickTimer.CreateFromSeconds(Runner, seconds);
        _chaosEndWarned = false;
        NextMeteor = TickTimer.CreateFromSeconds(Runner, 1f);
        Announce($"{ChaosInfo.Kind(modifier)}: {ChaosInfo.Title(modifier)}!");
        Debug.Log($"[Chaos] {modifier} for {seconds:0}s");
        OnChaosStarted(modifier);
    }

    /// <summary>
    /// Host only. A knocked-out contestant comes back as a ghost (<see cref="Player.IsSpectator"/>): free to run around
    /// <paramref name="spot"/>'s arena with everyone, unable to touch the game.
    /// </summary>
    protected void ReleaseAsGhost(Player player, Vector3 spot)
    {
        if (player == null) return;
        player.IsSpectator = true;
        if (player.Interaction != null) player.Interaction.ForceDrop();
        _playerSpawner.Teleport(player, spot);
        player.MovementLocked = false;
        player.ControlsLocked = false;
    }

    /// <summary>Host only. Ends the active chaos early (a twist that has done its job, e.g. one Double Dare turn).</summary>
    protected void StopChaos()
    {
        if (HasStateAuthority && ActiveChaos != ChaosModifier.None) EndChaos();
    }

    private void EndChaos()
    {
        var ended = ActiveChaos;
        ActiveChaos = ChaosModifier.None;
        ChaosTimer = TickTimer.None;
        if (IsGameActive) Announce($"{ChaosInfo.Title(ended)} OVER!");
        OnChaosEnded(ended);
    }

    /// <summary>Host only: the "ends in 3" warning went out for the running chaos.</summary>
    private bool _chaosEndWarned;

    private void TickChaos()
    {
        if (!IsGameActive)
        {
            if (ActiveChaos != ChaosModifier.None) EndChaos();
            PendingChaos = ChaosModifier.None;
            return;
        }

        if (PendingChaos != ChaosModifier.None && PendingTimer.Expired(Runner))
        {
            var next = PendingChaos;
            PendingChaos = ChaosModifier.None;
            ActivateChaos(next, PendingSeconds);
        }

        // Warn in the middle of the screen before it ends, so nobody has to watch the corner timer.
        if (ActiveChaos != ChaosModifier.None && !_chaosEndWarned && (ChaosTimer.RemainingTime(Runner) ?? 0f) <= CHAOS_WARNING
            && (ChaosTimer.RemainingTime(Runner) ?? 0f) > 0.5f)
        {
            _chaosEndWarned = true;
            Announce($"{ChaosInfo.Title(ActiveChaos)} ENDS IN 3...");
        }
        if (ActiveChaos != ChaosModifier.None && ChaosTimer.Expired(Runner))
            EndChaos();

        // Random director until the audience vote exists.
        if (ChaosDirector.Level != ChaosLevel.Off && ActiveChaos == ChaosModifier.None && PendingChaos == ChaosModifier.None)
        {
            if (!NextRandomChaos.IsRunning)
                NextRandomChaos = TickTimer.CreateFromSeconds(Runner, ChaosDirector.Level == ChaosLevel.Wild ? 8f : 18f);
            else if (NextRandomChaos.Expired(Runner))
            {
                NextRandomChaos = TickTimer.CreateFromSeconds(Runner, ChaosDirector.Level == ChaosLevel.Wild ? Random.Range(10f, 16f) : Random.Range(20f, 32f));
                // The game's own twist comes up about a quarter of the time; it is the most memorable chaos.
                float seconds = ChaosDirector.Level == ChaosLevel.Wild ? 12f : 10f;
                bool started = TwistTitle != null && Random.value < 0.25f && StartChaos(ChaosModifier.Twist, seconds);
                for (int attempt = 0; attempt < 8 && !started; attempt++)
                    started = StartChaos((ChaosModifier)Random.Range(1, ChaosInfo.COUNT), seconds);
            }
        }

        // Meteor shower: the host picks impact points near contestants, every peer shows the warning and the rock.
        if (ActiveChaos == ChaosModifier.MeteorShower && NextMeteor.ExpiredOrNotRunning(Runner))
        {
            NextMeteor = TickTimer.CreateFromSeconds(Runner, 0.7f);
            var targets = new List<Player>();
            foreach (var entry in _playerSpawner.Players)
                if (entry.Value != null && !entry.Value.ControlsLocked && !entry.Value.IsSpectator) targets.Add(entry.Value);
            if (targets.Count > 0)
            {
                var target = targets[Random.Range(0, targets.Count)];
                var flat = Random.insideUnitCircle * 4f;
                var point = target.transform.position + new Vector3(flat.x, 0f, flat.y);
                if (Runner.GetPhysicsScene().Raycast(point + Vector3.up * 6f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                    point = hit.point;
                _pendingMeteors.Add((point, TickTimer.CreateFromSeconds(Runner, METEOR_WARNING)));
                RPC_Meteor(point);
            }
        }
        for (int i = _pendingMeteors.Count - 1; i >= 0; i--)
        {
            if (!_pendingMeteors[i].timer.Expired(Runner)) continue;
            var impact = _pendingMeteors[i].point;
            _pendingMeteors.RemoveAt(i);
            foreach (var entry in _playerSpawner.Players)
            {
                var player = entry.Value;
                if (player == null || player.ControlsLocked || player.IsSpectator) continue;
                var away = player.transform.position - impact;
                if (away.magnitude > 2.6f) continue;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                if (player.TryGetComponent<PlayerMovement>(out var movement))
                    movement.Launch(away.normalized * 10f + Vector3.up * 8f);
            }
        }
    }

    public const float METEOR_WARNING = 1.2f;
    private readonly List<(Vector3 point, TickTimer timer)> _pendingMeteors = new();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Meteor(Vector3 point)
    {
        ChaosEffects.Meteor(point, METEOR_WARNING);
    }

    #endregion

    protected virtual void StartGame()
    {
        SetPhase(MinigamePhase.Playing, IsTimeLimited ? Definition.GameDuration * MatchSettings.LengthMultiplier : 0f);
        SetPlayersInput(true);

        Debug.Log($"Minigame {Definition.Id} started!");
    }

    /// <summary>Host only. Ends the round; <paramref name="placements"/> is ordered best first, one player per place.</summary>
    protected void FinishRound(IReadOnlyList<PlayerRef> placements)
    {
        FinishRound(placements.Select(p => (IReadOnlyList<PlayerRef>)new[] { p }).ToList());
    }

    /// <summary>Host only. Ends the round; <paramref name="standings"/> is ordered best first and players in one group tie.</summary>
    protected void FinishRound(IReadOnlyList<IReadOnlyList<PlayerRef>> standings)
    {
        if (!HasStateAuthority || !IsGameActive)
            return;

        SetPhase(MinigamePhase.Finished);
        SetPlayersInput(false);
        Debug.Log($"Minigame {Definition.Id} ended!");

        GameManager.SubmitRoundResult(standings);
    }

    /// <summary>Host only. Standings when the time limit runs out; by default every connected player ties.</summary>
    protected virtual IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings()
    {
        return new[] { (IReadOnlyList<PlayerRef>)ConnectedPlayers().ToList() };
    }

    protected IEnumerable<PlayerRef> ConnectedPlayers() => Runner.ActivePlayers.Where(GameManager.IsConnectedPlayer);

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
            TickChaos();

        if (!HasStateAuthority || !PhaseTimer.Expired(Runner))
            return;

        switch (Phase)
        {
            case MinigamePhase.Intro:
                BeginCountdown();
                break;
            case MinigamePhase.Countdown:
                StartGame();
                break;
            case MinigamePhase.Playing:
                Debug.Log($"Minigame {Definition.Id}: time is up");
                FinishRound(GetTimeUpStandings());
                break;
        }
    }

    public override void Render()
    {
        if (Phase != MinigamePhase.Countdown && !(Phase == MinigamePhase.Playing && IsTimeLimited))
            return;

        int seconds = Mathf.CeilToInt(TimeRemaining);
        if (seconds == _lastShownSecond || seconds <= 0)
            return;

        _lastShownSecond = seconds;
        if (Phase == MinigamePhase.Countdown)
            OnCountdownTick?.Invoke(seconds);
        else if (seconds <= FINAL_SECONDS)
            OnTimeLimitTick?.Invoke(seconds);
    }

    /// <summary>Host only. Input is gated in the simulation by the networked <see cref="Player.ControlsLocked"/>.</summary>
    protected void SetPlayersInput(bool value)
    {
        foreach (var playerEntry in _playerSpawner.Players)
        {
            if (playerEntry.Value)
                playerEntry.Value.ControlsLocked = !value;
        }
    }

    protected void SetPlayerInput(PlayerRef player, bool value)
    {
        if (_playerSpawner.Players.TryGet(player, out var playerObject) && playerObject)
            playerObject.ControlsLocked = !value;
    }

    private void SetPhase(MinigamePhase phase, float duration = 0f)
    {
        Phase = phase;
        PhaseTimer = duration > 0f ? TickTimer.CreateFromSeconds(Runner, duration) : TickTimer.None;
    }

    private void OnPhaseChanged()
    {
        _lastShownSecond = -1;
        switch (Phase)
        {
            case MinigamePhase.Countdown:
                OnCountdownStarted?.Invoke();
                break;
            case MinigamePhase.Playing:
                OnMinigameStart?.Invoke();
                break;
            case MinigamePhase.Finished:
                OnMinigameEnd?.Invoke();
                break;
        }
    }
}
