using Fusion;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Console Round (GDD): the contestants sit on bean bags in a living-room set, each in front of an old TV with a
/// console, and play one retro cartridge picked for the round (<see cref="ConsoleGame"/>), first person with a
/// gamepad in their hands. Every player's game runs on their own machine from a shared seed (same course for
/// everyone) and reports its score to the host; the best run of the round counts. Crash and you restart.
/// Chaos: speed, gravity, jump, icy controls, reversed controls and blackout (static) bend the console game;
/// its own twist, MIRROR MODE, flips the picture.
/// </summary>
public class ConsoleRoundMinigameController : MinigameController
{
    public const int STATIONS = 6;
    private const float STEP = 1f / 60f;
    private const float RESTART_SECONDS = 1.6f;

    [SerializeField, Tooltip("Origin of the TV row: stations along its X axis, seats toward its +Z")] private Transform _room;
    [SerializeField, Tooltip("One definition per cartridge (rules card texts and picture), indexed like ConsoleGame.Create")]
    private MinigameDefinitionSO[] _cartridges;
    [SerializeField] private GameObject _tvModel;
    [SerializeField] private GameObject _consoleModel;
    [SerializeField] private GameObject _seatModel;
    [SerializeField] private Material _material;
    [SerializeField] private Material _screenMaterial;
    [SerializeField] private float _spacing = 1.55f;
    [SerializeField] private float _seatDistance = 1.35f;
    [SerializeField] private float _tvScale = 1.2f;
    [SerializeField, Tooltip("Screen centre and size in the TV model's local space (before _tvScale)")]
    private Vector3 _screenOffset = new(0f, 0.47f, 0.25f);
    [SerializeField] private Vector2 _screenSize = new(0.56f, 0.42f);
    [SerializeField] private float _tvYaw = 0f;
    [SerializeField] private float _eyeHeight = 1.25f;
    [SerializeField, Range(0.2f, 1f), Tooltip("Snake Pit: step time while the direction you're going is held (0.5 = twice as fast)")]
    private float _snakeHoldSpeedUp = 0.5f;

    [Networked] public byte Cartridge { get; private set; }
    [Networked] private int Seed { get; set; }
    [Networked, Capacity(8)] private NetworkDictionary<PlayerRef, int> Best => default;
    [Networked, Capacity(8)] private NetworkDictionary<PlayerRef, int> Live => default;
    /// <summary>Bit per <see cref="PlayerRef.PlayerId"/>: their current run has ended (waiting to restart).</summary>
    [Networked] private int CrashedMask { get; set; }

    private readonly List<PlayerRef> _order = new();
    private PixelScreen[] _screens;
    private Renderer[] _screenRenderers;
    private Transform[] _stationRoots;
    private ConsoleGame _game;
    private byte _gameCartridge;
    private int _run, _bestLocal, _reportedScore = -1;
    private bool _reportedCrash;
    private float _accumulator, _restartTimer, _nextRemoteDraw, _nextReport;
    private ConsoleViewmodel _viewmodel;
    private ConsoleInput _input;
    private bool _prevA, _prevUp, _bodyHidden, _reversedDown;

    /// <summary>Test hook: when 0 or more, the host plays this cartridge and the next one in the following round.</summary>
    public static int NextTestCartridge = -1;
    /// <summary>Test hook: overrides the local gamepad (editor test scripts).</summary>
    public static ConsoleInput? TestInput;
    public string DebugState => $"cart={Cartridge} run={_run} score={(_game != null ? _game.Score : -1)} over={(_game != null && _game.Over)} best={BestOf(Runner.LocalPlayer)} station={StationOf(Runner.LocalPlayer)} vm={(_viewmodel != null && _viewmodel.isActiveAndEnabled)}";

    public override MinigameDefinitionSO Definition =>
        _cartridges != null && Object != null && Object.IsValid && Cartridge < _cartridges.Length && _cartridges[Cartridge] != null
            ? _cartridges[Cartridge] : _minigameDefinition;

    public override bool UsesDefaultControls => false;
    public override bool AllowShove => false;
    public override string TwistTitle => "MIRROR MODE";
    public override string TwistKind => "DEBUFF";
    public override string TwistDescription => "Every screen flips left to right.";

    /// <summary>
    /// Only chaos the console game can feel: speed, reversed controls, blackout (static) and the twist everywhere;
    /// low gravity and super jump only in cartridges with jumping. Body chaos (meteors, giants, tiny, ice) is out:
    /// everyone is seated.
    /// </summary>
    public override bool SupportsChaos(ChaosModifier modifier)
    {
        if (!base.SupportsChaos(modifier)) return false;
        return modifier switch
        {
            ChaosModifier.SpeedRush or ChaosModifier.SlowMotion or ChaosModifier.Blackout or ChaosModifier.Twist => true,
            // Playtesters: no reversed controls in Flap Dash or Pixel Runner (one-button timing games); Stack Tower is
            // one button too, where reversing would change nothing.
            ChaosModifier.ReversedControls => Cartridge is not (1 or 2 or 7),
            ChaosModifier.LowGravity or ChaosModifier.SuperJump => ConsoleGame.HasGravity(Cartridge),
            _ => false,
        };
    }

    /// <summary>Fastest honest scoring per cartridge (points per second): snake, flap, runner, bricks, hopper, astro, frog, stack.</summary>
    private static readonly float[] MaxRate = { 6f, 2.5f, 14f, 8f, 30f, 6f, 8f, 4f };

    public int BestOf(PlayerRef player) => Best.TryGet(player, out var v) ? v : 0;

    #region Layout (same on every peer)

    private Vector3 StationLocal(int station) => new((station - (STATIONS - 1) * 0.5f) * _spacing, 0f, 0f);
    public Vector3 SeatPosition(int station) => _room.TransformPoint(StationLocal(station) + new Vector3(0f, 0f, _seatDistance));
    public Vector3 ScreenCentre(int station) => _stationRoots != null && _screenRenderers[station] != null
        ? _screenRenderers[station].transform.position
        : _room.TransformPoint(StationLocal(station) + new Vector3(0f, 0.55f + _screenOffset.y * _tvScale, _screenOffset.z * _tvScale));
    public Vector3 EyePosition(int station) => SeatPosition(station) + Vector3.up * _eyeHeight + _room.forward * 0.3f;

    /// <summary>Contestants use the middle stations: two players get the two centre TVs.</summary>
    private int StationOf(int slot, int count) => slot + (STATIONS - count) / 2;

    private List<PlayerRef> SortedPlayers()
    {
        _order.Clear();
        if (_playerSpawner == null || _playerSpawner.Object == null) return _order;
        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null) _order.Add(entry.Key);
        _order.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return _order;
    }

    private PlayerRef OwnerOf(int station)
    {
        var players = SortedPlayers();
        int slot = station - (STATIONS - players.Count) / 2;
        return slot >= 0 && slot < players.Count ? players[slot] : PlayerRef.None;
    }

    private int StationOf(PlayerRef player)
    {
        var players = SortedPlayers();
        int slot = players.IndexOf(player);
        return slot < 0 ? -1 : StationOf(slot, players.Count);
    }

    #endregion

    public override void Spawned()
    {
        base.Spawned();
        SnakeGame.HoldSpeedUp = _snakeHoldSpeedUp;
        if (HasStateAuthority)
        {
            int count = Mathf.Min(ConsoleGame.COUNT, _cartridges != null && _cartridges.Length > 0 ? _cartridges.Length : ConsoleGame.COUNT);
            Cartridge = (byte)(NextTestCartridge >= 0 ? NextTestCartridge++ % count : Random.Range(0, count));
            Seed = Random.Range(1, int.MaxValue);
        }
        BuildStations();
    }

    private void BuildStations()
    {
        _screens = new PixelScreen[STATIONS];
        _screenRenderers = new Renderer[STATIONS];
        _stationRoots = new Transform[STATIONS];
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < STATIONS; i++)
        {
            var root = new GameObject($"Station {i}").transform;
            root.SetParent(_room, false);
            root.localPosition = StationLocal(i);
            _stationRoots[i] = root;

            if (_tvModel != null)
            {
                var tv = Instantiate(_tvModel, root).transform;
                tv.localPosition = new Vector3(0f, 0.55f, 0f);
                tv.localRotation = Quaternion.Euler(0f, _tvYaw, 0f);
                tv.localScale = Vector3.one * _tvScale;
            }
            if (_consoleModel != null)
            {
                var console = Instantiate(_consoleModel, root).transform;
                console.localPosition = new Vector3(0.42f, 0.55f, 0.32f);
                console.localRotation = Quaternion.Euler(0f, 180f - 15f, 0f);
                console.localScale = Vector3.one * 0.9f;
            }
            if (_seatModel != null)
            {
                var seat = Instantiate(_seatModel, root).transform;
                seat.localPosition = new Vector3(0f, 0f, _seatDistance + 0.25f);
                seat.localRotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (var c in seat.GetComponentsInChildren<Collider>()) Destroy(c);
            }

            // The picture: a quad over the TV's glass, its own texture per station.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Screen";
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = new Vector3(0f, 0.55f, 0f) + Quaternion.Euler(0f, _tvYaw, 0f) * (_screenOffset * _tvScale);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(_screenSize.x * _tvScale, _screenSize.y * _tvScale, 1f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = _screenMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _screens[i] = new PixelScreen(ConsoleGame.W, ConsoleGame.H);
            renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", _screens[i].Texture);
            renderer.SetPropertyBlock(block);
            _screenRenderers[i] = renderer;

            // A soft TV glow on the contestant.
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0f, 1.1f, 0.6f);
            glow.type = LightType.Point;
            glow.range = 2.6f;
            glow.intensity = 1.2f;
            glow.color = new Color(0.55f, 0.75f, 1f);
            glow.shadows = LightShadows.None;
        }
    }

    protected override void OnIntro()
    {
        // Seat everyone in front of their TV, facing it; nobody walks around in this round.
        var players = SortedPlayers();
        for (int i = 0; i < players.Count; i++)
        {
            if (!_playerSpawner.Players.TryGet(players[i], out var player) || player == null) continue;
            int station = StationOf(i, players.Count);
            _playerSpawner.Teleport(player, SeatPosition(station), Quaternion.LookRotation(-_room.forward));
            player.MovementLocked = true;
        }
    }

    protected override void StartGame()
    {
        base.StartGame();
        if (!HasStateAuthority) return;
        foreach (var p in ConnectedPlayers())
        {
            Best.Set(p, 0);
            Live.Set(p, 0);
        }
        CrashedMask = 0;
    }

    public override string GetStatusText()
    {
        if (!IsGameActive) return null;
        PlayerRef leader = PlayerRef.None;
        int best = -1;
        foreach (var pair in Best)
            if (pair.Value > best) { best = pair.Value; leader = pair.Key; }
        string leaderName = leader == Runner.LocalPlayer ? "you" : Nickname(leader);
        return $"Your best <b>{BestOf(Runner.LocalPlayer)}</b>   <size=80%>top score: {Mathf.Max(0, best)} ({leaderName})</size>";
    }

    private string Nickname(PlayerRef player) =>
        _playerSpawner != null && _playerSpawner.Object != null && _playerSpawner.Players.TryGet(player, out var p) && p != null && !string.IsNullOrEmpty(p.Nickname)
            ? p.Nickname : $"Player {player.PlayerId}";

    /// <summary>Each contestant's own console reports its score (the host trusts it: it is a party game).</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Report(int score, NetworkBool crashed, RpcInfo info = default)
    {
        if (!IsGameActive) return;
        var player = info.Source == PlayerRef.None ? Runner.LocalPlayer : info.Source;
        // Plausibility cap: no cartridge scores faster than MaxRate per second (a client reported 9999 in playtest).
        float elapsed = Definition.GameDuration * MatchSettings.LengthMultiplier - TimeRemaining;
        score = Mathf.Clamp(score, 0, Mathf.CeilToInt((elapsed + 2f) * MaxRate[Mathf.Min(Cartridge, MaxRate.Length - 1)]));
        Live.Set(player, score);
        if (score > BestOf(player)) Best.Set(player, score);
        int bit = 1 << player.PlayerId;
        CrashedMask = crashed ? CrashedMask | bit : CrashedMask & ~bit;
    }

    protected override IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings() =>
        ConnectedPlayers()
            .GroupBy(BestOf)
            .OrderByDescending(g => g.Key)
            .Select(g => (IReadOnlyList<PlayerRef>)g.ToList())
            .ToList();

    #region Local console (every peer runs only its own contestant's game)

    private void Update()
    {
        if (Object == null || !Object.IsValid || _screens == null) return;

        int mine = StationOf(Runner.LocalPlayer);
        UpdateLocalGame(mine);
        UpdateCamera(mine);

        // Everyone else's TV shows their scoreboard, a few times a second.
        if (Time.unscaledTime >= _nextRemoteDraw)
        {
            _nextRemoteDraw = Time.unscaledTime + 0.25f;
            for (int i = 0; i < STATIONS; i++)
            {
                if (i == mine) continue;
                DrawRemote(i, OwnerOf(i));
                _screens[i].Upload();
            }
        }
    }

    private void UpdateLocalGame(int station)
    {
        if (station < 0) return;
        var screen = _screens[station];
        screen.MirrorX = TwistActive;
        // The cartridge replicates a moment after spawning on clients: swap the game if it changed.
        if (_game == null || _gameCartridge != Cartridge)
        {
            _game = ConsoleGame.Create(Cartridge);
            _gameCartridge = Cartridge;
            _run = 0;
        }
        ReadInput();

        switch (Phase)
        {
            case MinigamePhase.Playing:
                if (_run == 0) StartRun();
                var chaos = ActiveChaos;
                float speed = chaos == ChaosModifier.SpeedRush ? 1.35f : chaos == ChaosModifier.SlowMotion ? 0.6f : 1f;
                var tuning = new ConsoleTuning
                {
                    Gravity = chaos == ChaosModifier.LowGravity ? 0.5f : 1f,
                    Jump = chaos == ChaosModifier.SuperJump ? 1.3f : chaos == ChaosModifier.LowGravity ? 0.8f : 1f,
                };
                var input = _input;
                if (chaos == ChaosModifier.ReversedControls)
                {
                    // Only the directions flip (left/right, up/down); Space stays the action/jump button.
                    bool down = input.Y < 0;
                    input.X = -input.X;
                    input.Y = -input.Y;
                    input.UpPressed = down && !_reversedDown;
                    _reversedDown = down;
                }

                if (_game.Over)
                {
                    _input.APressed = _input.UpPressed = false;   // taps during GAME OVER don't carry into the next run
                    _restartTimer -= Time.deltaTime;
                    if (_restartTimer <= 0f) StartRun();
                }
                else
                {
                    _accumulator = Mathf.Min(_accumulator + Time.deltaTime * speed, 0.25f);
                    while (_accumulator >= STEP)
                    {
                        _accumulator -= STEP;
                        _game.Step(STEP, input, tuning);
                        // A press is consumed by one step: one tap is one flap / jump (frames without a step keep it).
                        input.APressed = input.UpPressed = false;
                        _input.APressed = _input.UpPressed = false;
                        if (_game.Over)
                        {
                            _restartTimer = RESTART_SECONDS;
                            Sfx.Play(Sound.Nope, 0.7f, 0f);
                            break;
                        }
                    }
                }
                _bestLocal = Mathf.Max(_bestLocal, _game.Score);
                _game.Best = Mathf.Max(_bestLocal, BestOf(Runner.LocalPlayer));
                _game.Draw(screen);
                if (_game.Over) DrawGameOver(screen);
                if (chaos == ChaosModifier.Blackout) { screen.Dim(0.6f); screen.Static(0.04f, (uint)Time.frameCount); }
                Report();
                break;

            case MinigamePhase.Finished:
                DrawTitle(screen, "TIME UP!", Mathf.Max(_bestLocal, BestOf(Runner.LocalPlayer)));
                break;

            default:
                DrawTitle(screen, Phase == MinigamePhase.Countdown ? null : "GET READY", -1);
                break;
        }
        screen.Upload();
    }

    private void StartRun()
    {
        _run++;
        // Same seed sequence for everyone: every contestant's n-th run is the same course.
        _game.Begin(Seed + _run * 7919);
        _accumulator = 0f;
    }

    private void Report()
    {
        bool crashed = _game.Over;
        if ((_game.Score == _reportedScore && crashed == _reportedCrash) || Time.unscaledTime < _nextReport) return;
        _reportedScore = _game.Score;
        _reportedCrash = crashed;
        _nextReport = Time.unscaledTime + 0.1f;
        RPC_Report(_game.Score, crashed);
    }

    private void ReadInput()
    {
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        int x = 0, y = 0;
        bool a = false, up = false;
        if (keyboard != null && !PauseMenu.IsOpen)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) { y += 1; up = true; }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1;
            a |= keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed;
        }
        if (pad != null)
        {
            var stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
            if (stick.x < -0.5f) x = -1; else if (stick.x > 0.5f) x = 1;
            if (stick.y < -0.5f) y = -1; else if (stick.y > 0.5f) { y = 1; up = true; }
            a |= pad.buttonSouth.isPressed;
        }
        // Edges accumulate until the next simulation step consumes them.
        if (TestInput.HasValue) { x = TestInput.Value.X; y = TestInput.Value.Y; a = TestInput.Value.A; up = y > 0; }
        _input.APressed |= a && !_prevA;
        _input.UpPressed |= up && !_prevUp;
        _prevA = a;
        _prevUp = up;
        _input.X = x;
        _input.Y = y;
        _input.A = a;
        if (_viewmodel != null) _viewmodel.SetPressed(x, y, a);
    }

    #endregion

    #region Screens

    private static Color32 C(byte r, byte g, byte b) => new(r, g, b, 255);

    private void DrawTitle(PixelScreen s, string line, int best)
    {
        string title = _game != null ? _game.Title : "";
        s.Clear(C(46, 26, 71));
        float t = Time.time;
        for (int i = 0; i < 12; i++)
            s.Rect((int)Mathf.Repeat(i * 23f + t * 10f, ConsoleGame.W + 10f) - 10, 8 + (i * 17) % 80, 2, 2, C(90, 60, 130));
        s.TextShadowCentered(20, title, C(255, 202, 61), C(232, 89, 12), 2);
        if (Phase == MinigamePhase.Countdown)
        {
            int n = Mathf.CeilToInt(TimeRemaining);
            s.TextShadowCentered(44, n > 0 ? n.ToString() : "GO!", C(255, 255, 255), C(232, 89, 12), 4);
        }
        else if (line != null && (((int)(t * 2.5f)) & 1) == 0)
            s.TextShadowCentered(48, line, C(255, 255, 255), C(20, 10, 40));
        if (best >= 0)
        {
            s.TextCentered(64, "YOUR BEST", C(200, 180, 255));
            int w = PixelScreen.NumberWidth(best, 2);
            s.Number((ConsoleGame.W - w) / 2, 72, best, C(255, 255, 255), 2);
        }
        else
            s.TextCentered(80, "PAR-TV ENTERTAINMENT SYSTEM", C(140, 110, 190));
    }

    private void DrawGameOver(PixelScreen s)
    {
        s.Rect(24, 34, 80, 30, C(20, 10, 40));
        s.Frame(24, 34, 80, 30, C(255, 94, 91));
        s.TextCentered(39, "GAME OVER", C(255, 94, 91), 2);
        s.TextCentered(53, "RETRYING...", C(220, 210, 240));
    }

    private void DrawRemote(int station, PlayerRef owner)
    {
        var s = _screens[station];
        s.MirrorX = false;
        if (owner == PlayerRef.None)
        {
            s.Clear(C(30, 30, 36));
            s.Static(0.55f, (uint)(Time.frameCount * 31 + station));
            s.Rect(30, 42, 68, 11, C(20, 20, 26));
            s.TextCentered(45, "NO SIGNAL", C(220, 220, 230));
            return;
        }

        s.Clear(C(46, 26, 71));
        s.TextCentered(4, _game != null ? _game.Title : "CONSOLE", C(255, 202, 61));
        string name = Nickname(owner).ToUpperInvariant();
        if (name.Length > 14) name = name.Substring(0, 14);
        s.TextCentered(16, name, C(255, 255, 255));
        if (IsGameActive)
        {
            int live = Live.TryGet(owner, out var v) ? v : 0;
            bool crashed = (CrashedMask & (1 << owner.PlayerId)) != 0;
            int w = PixelScreen.NumberWidth(live, 4);
            s.Number((ConsoleGame.W - w) / 2, 34, live, crashed ? C(255, 94, 91) : C(255, 255, 255), 4);
            s.TextCentered(62, crashed ? "GAME OVER" : "PLAYING", crashed ? C(255, 94, 91) : C(79, 209, 160));
        }
        else
            s.TextCentered(46, Phase == MinigamePhase.Finished ? "TIME UP!" : "GET READY", C(200, 180, 255));
        s.Text(8, 80, "BEST", C(200, 180, 255));
        s.Number(28, 80, BestOf(owner), C(255, 255, 255));
    }

    #endregion

    #region First-person view

    private void UpdateCamera(int station)
    {
        bool firstPerson = station >= 0 && Phase is MinigamePhase.Countdown or MinigamePhase.Playing or MinigamePhase.Finished;
        if (!firstPerson)
        {
            if (_viewmodel != null) _viewmodel.gameObject.SetActive(false);
            return;
        }
        if (_viewmodel == null)
        {
            var hands = Player.Local != null && CosmeticsCatalog.Instance != null ? CosmeticsCatalog.Instance.GetColor(Player.Local.HandsColor).Color : new Color(1f, 0.85f, 0.7f);
            var sleeves = Player.Local != null ? Player.Local.PlayerColor : new Color(0.6f, 0.4f, 0.9f);
            _viewmodel = ConsoleViewmodel.Create(_material, hands, sleeves);
        }
        _viewmodel.gameObject.SetActive(true);
        _viewmodel.Place(EyePosition(station), ScreenCentre(station));
        if (!_bodyHidden && Player.Local != null)
        {
            ConsoleViewmodel.HideBody(Player.Local, true);
            _bodyHidden = true;
        }
    }

    #endregion

    public override void Render()
    {
        base.Render();
        // Seated pose for every contestant (seen in the intro and by the cameras of the show).
        if (_playerSpawner == null || _playerSpawner.Object == null) return;
        foreach (var entry in _playerSpawner.Players)
            if (entry.Value != null && !entry.Value.TryGetComponent<ConsoleSitter>(out _))
                entry.Value.gameObject.AddComponent<ConsoleSitter>();
    }

    public override bool TryGetIntroShot(float time, out Vector3 position, out Vector3 lookAt)
    {
        // A slow dolly behind the bean bags, like a sitcom camera.
        float sweep = Mathf.PingPong(time / 7f, 1f);
        position = _room.TransformPoint(new Vector3(Mathf.Lerp(-3.2f, 3.2f, Mathf.SmoothStep(0f, 1f, sweep)), 2.3f, _seatDistance + 3.6f));
        lookAt = _room.TransformPoint(new Vector3(0f, 0.9f, 0.2f));
        return true;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (_viewmodel != null) Destroy(_viewmodel.gameObject);
        if (_screens != null)
            foreach (var s in _screens)
                if (s != null) Destroy(s.Texture);
    }
}
