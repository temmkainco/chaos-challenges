using System.Collections.Generic;
using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-round HUD shared by every minigame, built with the UI kit: the round tag top left, the show timer (a quiet
/// bar, a big count in the last seconds), the game's status line and its scoreboard top right, chaos as a lower third
/// while it is coming and while it lasts, a stamped banner for "YOUR TURN!" and the announcer's lines
/// (<see cref="MinigameController.Announcement"/>), the game's big call-out (Color Code's colour) and a one-line hint
/// for the first seconds. Only what you need now; it reads replicated state every frame.
/// </summary>
public class MinigameHud : MonoBehaviour
{
    private const float HINT_SECONDS = 7f, STATUS_MAX_WIDTH = 560f, BOARD_WIDTH = 460f;

    private CanvasGroup _group;
    private RectTransform _safe;
    private LowerThird _roundTag, _chaos;
    private ShowTimer _timer;
    private UiBox _statusBox, _hintBox;
    private TextMeshProUGUI _status, _hint, _banner, _callout;
    private CanvasGroup _bannerGroup, _hintGroup;
    private RectTransform _board;
    private readonly List<(GameObject root, Image disc, TextMeshProUGUI number, TextMeshProUGUI name, TextMeshProUGUI value)> _rows = new();
    private readonly Queue<(string text, bool chaos)> _bannerQueue = new();
    private bool _bannerUp;
    private float _playingTime, _nextStatus;
    private PlayerRef _lastTurn = PlayerRef.None;
    private MinigameController _shownFor;
    private int _lastAnnouncement, _lastTurnSecond = -1;
    private string _lastStatus;
    private static BasePlayerSpawner _spawner;

    private void Awake()
    {
        _safe = Ui.Canvas("Minigame HUD", 25, transform);
        _group = _safe.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        _roundTag = LowerThird.Create(_safe, true);
        _roundTag.Dock(new Vector2(0f, 1f), new Vector2(0f, -34f));
        _timer = ShowTimer.Create(_safe);

        _statusBox = Ui.Box(_safe, "Status", UiTheme.Tone.Studio);
        Ui.Place((RectTransform)_statusBox.transform, new Vector2(1f, 1f), Vector2.zero, new Vector2(360f, 72f));
        _status = Ui.Text(_statusBox.transform, "", UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Center);
        _status.GetComponent<ThemedText>().ShrinkToFit = false;
        _status.GetComponent<ThemedText>().Apply();
        Ui.Stretch(_status.rectTransform, 14f);

        _board = Ui.Box(_safe, "Scoreboard", UiTheme.Tone.Studio).GetComponent<RectTransform>();
        Ui.Place(_board, new Vector2(1f, 1f), new Vector2(0f, -90f), new Vector2(BOARD_WIDTH, 60f));
        var layout = Ui.Stack<VerticalLayoutGroup>(_board, 0, TextAnchor.UpperLeft);
        layout.padding = new RectOffset(16, 18, 12, 12);
        _board.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _board.gameObject.SetActive(false);

        _chaos = LowerThird.Create(_safe);

        _callout = Ui.Text(_safe, "", UiTheme.TextRole.DisplayL, UiTheme.Tone.Paper, TextAlignmentOptions.Center, true);
        Ui.Place(_callout.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 130f));
        _callout.gameObject.SetActive(false);

        _banner = Ui.Text(_safe, "", UiTheme.TextRole.DisplayXL, UiTheme.Tone.Accent, TextAlignmentOptions.Center, true);
        Ui.Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1600f, 180f));
        _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
        _banner.gameObject.SetActive(false);

        _hintBox = Ui.Box(_safe, "Hint", UiTheme.Tone.Studio);
        Ui.Place((RectTransform)_hintBox.transform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(900f, 72f));
        _hintGroup = _hintBox.gameObject.AddComponent<CanvasGroup>();
        _hintGroup.alpha = 0f;
        _hint = Ui.Text(_hintBox.transform, "", UiTheme.TextRole.Body, UiTheme.Tone.Paper);
        Ui.Stretch(_hint.rectTransform, 12f);

        CreateRulesCard();
        gameObject.AddComponent<ChaosEffects>();
    }

    /// <summary>The rules card builds its own overlay canvas: this HUD is hidden during the intro.</summary>
    private void CreateRulesCard()
    {
        var go = new GameObject("RulesCard");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        go.AddComponent<RulesCard>();
    }

    private void Update()
    {
        var controller = MinigameController.Current;
        bool visible = controller != null && controller.Object != null && controller.Object.IsValid
                       && (controller.Phase == MinigamePhase.Countdown || controller.Phase == MinigamePhase.Playing)
                       && !PauseMenu.IsOpen;
        _group.alpha = Mathf.MoveTowards(_group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (!visible)
        {
            if (controller == null || controller.Object == null || !controller.Object.IsValid || controller.Phase != MinigamePhase.Countdown) _chaos.Unpin();
            return;
        }

        if (controller != _shownFor)
        {
            _shownFor = controller;
            _playingTime = 0f;
            _lastTurn = PlayerRef.None;
            _hint.text = "";
            _lastAnnouncement = controller.AnnouncementId;
            ShowRoundTag(controller);
        }

        if (controller.AnnouncementId != _lastAnnouncement)
        {
            _lastAnnouncement = controller.AnnouncementId;
            string text = controller.Announcement.ToString();
            // "SPEED RUSH IN 3..." is the chaos lower third's job (it counts down live); the start and end still stamp.
            if (!text.Contains(" IN 3")) _bannerQueue.Enqueue((text, IsChaosLine(text)));
        }
        // One banner at a time: chaos warnings used to wipe out Color Code's call before anyone read it.
        if (_bannerQueue.Count > 0 && !_bannerUp)
        {
            var (text, chaos) = _bannerQueue.Dequeue();
            PopBanner(text, chaos ? UiTheme.Tone.Paper : UiTheme.Tone.Accent, chaos ? 2.2f : 1.1f);
        }

        // A persistent big call-out for the minigame's current instruction (Color Code's colour).
        string callout = controller.IsGameActive ? controller.Callout : null;
        bool showCallout = !string.IsNullOrEmpty(callout);
        if (_callout.gameObject.activeSelf != showCallout) _callout.gameObject.SetActive(showCallout);
        if (showCallout && _callout.text != callout) { _callout.text = callout; UiMotion.Punch(_callout.rectTransform, 0.2f); }

        if (controller.IsGameActive) _playingTime += Time.deltaTime;

        // The timer bar every frame (it is cheap); the rest four times a second.
        bool timed = controller.IsGameActive && controller.IsTimeLimited;
        if (_timer.gameObject.activeSelf != timed) _timer.gameObject.SetActive(timed);
        if (timed) _timer.Set(controller.TimeRemaining, controller.Definition.GameDuration * MatchSettings.LengthMultiplier);

        if (Time.unscaledTime >= _nextStatus)
        {
            _nextStatus = Time.unscaledTime + 0.25f;
            UpdateStatus(controller);
            UpdateChaos(controller);
            UpdateBoard(controller);
        }

        var definition = controller.Definition;
        if (_hint.text.Length == 0 && definition != null && definition.Rules != null && definition.Rules.Length > 0)
            _hint.text = definition.Rules[0];
        bool showHint = GameSettings.ShowHints && controller.IsGameActive && _playingTime < HINT_SECONDS && _hint.text.Length > 0;
        _hintGroup.alpha = Mathf.MoveTowards(_hintGroup.alpha, showHint ? 1f : 0f, Time.deltaTime * 2f);
    }

    /// <summary>ROUND 2/5 · PRIZE GRAB, top left for the whole round.</summary>
    private void ShowRoundTag(MinigameController controller)
    {
        var gameManager = NetworkGameManager.Instance;
        string round = gameManager != null && gameManager.RoundCount > 0 ? $"ROUND {gameManager.RoundIndex + 1}/{gameManager.RoundCount}" : "ROUND";
        string name = controller.Definition != null ? controller.Definition.DisplayName.ToUpperInvariant() : "";
        _roundTag.Pin(round, name);
    }

    private void UpdateStatus(MinigameController controller)
    {
        string status = controller.IsGameActive ? controller.GetStatusText() ?? "" : "";
        if (controller.IsGameActive && controller is TurnBasedMinigameController turnBased && turnBased.CurrentTurn != PlayerRef.None)
        {
            bool mine = turnBased.CurrentTurn == controller.Runner.LocalPlayer;
            string turn = mine ? $"<color=#{Hex(UiTheme.Instance.Accent)}>Your turn</color>" : $"{Nickname(turnBased.CurrentTurn)}'s turn";
            if (turnBased.HasTurnTimer)
            {
                int left = Mathf.CeilToInt(turnBased.TurnTimeRemaining);
                turn += $"  {left}s";
                // The last seconds of a turn tick (louder on yours) and count down big on your screen.
                if (left <= 3 && left > 0 && left != _lastTurnSecond)
                {
                    Sfx.Play(Sound.Tick, mine ? 1f : 0.4f, 0f);
                    if (mine) PopBanner(left.ToString(), UiTheme.Tone.Bad, 0.6f);
                }
                _lastTurnSecond = left;
            }
            status = string.IsNullOrEmpty(status) ? turn : $"{turn}\n<size=80%>{status}</size>";

            if (turnBased.CurrentTurn != _lastTurn)
            {
                _lastTurn = turnBased.CurrentTurn;
                if (mine) PopBanner("YOUR TURN!", UiTheme.Tone.Accent, 1.1f);
            }
        }
        if (status == _lastStatus) return;
        _lastStatus = status;
        _status.text = status;
        bool show = status.Length > 0;
        if (_statusBox.gameObject.activeSelf != show) _statusBox.gameObject.SetActive(show);
        if (!show) return;
        // The box fits its text, up to a width; long status lines wrap.
        var size = _status.GetPreferredValues(status, STATUS_MAX_WIDTH - 28f, 0f);
        ((RectTransform)_statusBox.transform).sizeDelta = new Vector2(Mathf.Clamp(size.x + 40f, 200f, STATUS_MAX_WIDTH), size.y + 32f);
    }

    /// <summary>Chaos coming ("SPEED RUSH in 3") and then running ("SPEED RUSH 8s"), as a pinned lower third.</summary>
    private void UpdateChaos(MinigameController controller)
    {
        var chaos = controller.PendingChaos != ChaosModifier.None ? controller.PendingChaos : controller.ActiveChaos;
        if (chaos == ChaosModifier.None)
        {
            _chaos.Unpin();
            return;
        }
        string kind = ChaosInfo.Kind(chaos);
        var tone = kind == "BUFF" ? UiTheme.Tone.Good : kind == "HAZARD" ? UiTheme.Tone.Bad : UiTheme.Tone.Brand;
        string value = controller.PendingChaos != ChaosModifier.None
            ? $"{Cap(kind)} in {Mathf.CeilToInt(controller.PendingTimeRemaining)}"
            : $"{Cap(kind)} · {Mathf.CeilToInt(controller.ChaosTimeRemaining)}s left";
        _chaos.Pin(ChaosInfo.Title(chaos), value, tone);
    }

    private static string Cap(string kind) => kind.Length == 0 ? "" : kind.Substring(0, 1) + kind.Substring(1).ToLowerInvariant();

    /// <summary>
    /// Under the status line: one row per contestant (number disc in their colour, name, value), best first. Shown
    /// when the minigame reports values (<see cref="MinigameController.ScoreboardValue"/>).
    /// </summary>
    private void UpdateBoard(MinigameController controller)
    {
        var players = new List<(PlayerRef player, string value, float rank)>();
        foreach (var player in controller.Runner.ActivePlayers)
        {
            string value = controller.ScoreboardValue(player);
            if (value != null) players.Add((player, value, controller.ScoreboardRank(player)));
        }
        players.Sort((a, b) => b.rank.CompareTo(a.rank));
        bool show = players.Count > 0;
        if (_board.gameObject.activeSelf != show) _board.gameObject.SetActive(show);
        if (!show) return;
        // Always just under the status box, whose height changes with its lines.
        var status = (RectTransform)_statusBox.transform;
        float top = _statusBox.gameObject.activeSelf ? -status.sizeDelta.y - 16f : 0f;
        _board.anchoredPosition = new Vector2(0f, top);
        while (_rows.Count < players.Count) _rows.Add(CreateRow());
        var match = NetworkGameManager.Instance;
        if (_spawner == null || _spawner.Object == null) _spawner = FindFirstObjectByType<BasePlayerSpawner>();
        for (int i = 0; i < _rows.Count; i++)
        {
            bool on = i < players.Count;
            _rows[i].root.SetActive(on);
            if (!on) continue;
            var (player, value, _) = players[i];
            bool mine = player == controller.Runner.LocalPlayer;
            int colour = match != null && match.Object != null && match.Object.IsValid ? match.ColorIndexOf(player) : -1;
            _rows[i].name.text = mine ? $"{PlainName(player)} (you)" : PlainName(player);
            _rows[i].name.fontStyle = mine ? FontStyles.Bold : FontStyles.Normal;
            _rows[i].value.text = value;
            _rows[i].number.text = colour >= 0 ? (colour + 1).ToString() : "";
            _rows[i].disc.color = _spawner != null && _spawner.Object != null && _spawner.Players.TryGet(player, out var p) && p != null ? p.PlayerColor : Color.grey;
        }
    }

    private (GameObject, Image, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI) CreateRow()
    {
        var theme = UiTheme.Instance;
        var row = Ui.Rect(_board, "Row");
        Ui.Size(row, BOARD_WIDTH - 34f, 48f);
        var ring = Ui.Circle(row, "Ring", 40f, theme.Ink);
        Ui.Place(ring.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
        var disc = Ui.Circle(ring.transform, "Disc", 34f, Color.white);
        var number = Ui.Text(disc.transform, "", UiTheme.TextRole.Label, UiTheme.Tone.Ink);
        number.GetComponent<ThemedText>().ShrinkToFit = false;
        number.GetComponent<ThemedText>().Spacing = 0f;
        number.GetComponent<ThemedText>().Apply();
        Ui.Stretch(number.rectTransform);
        var name = Ui.Text(row, "", UiTheme.TextRole.Label, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        name.GetComponent<ThemedText>().SingleLine = true;
        name.GetComponent<ThemedText>().Apply();
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one;
        name.rectTransform.offsetMin = new Vector2(52f, 0f); name.rectTransform.offsetMax = new Vector2(-84f, 0f);
        var value = Ui.Text(row, "", UiTheme.TextRole.Body, UiTheme.Tone.Accent, TextAlignmentOptions.Right);
        value.fontStyle = FontStyles.Bold;
        value.GetComponent<ThemedText>().SingleLine = true;
        value.GetComponent<ThemedText>().Apply();
        Ui.Place(value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(80f, 48f));
        return (row.gameObject, disc, number, name, value);
    }

    private static bool IsChaosLine(string text) =>
        text.Contains(" IN 3") || text.EndsWith(" OVER!") || text.StartsWith("BUFF:") || text.StartsWith("DEBUFF:") || text.StartsWith("HAZARD:");

    /// <summary>A big stamped line in the upper middle ("YOUR TURN!", the announcer), one at a time.</summary>
    private void PopBanner(string text, UiTheme.Tone tone, float hold)
    {
        _bannerUp = true;
        _banner.text = text;
        _banner.GetComponent<ThemedText>().Set(UiTheme.TextRole.DisplayXL, tone, true);
        _banner.gameObject.SetActive(true);
        UiMotion.Stamp(_banner.rectTransform, _bannerGroup)
            .AppendInterval(hold)
            .Append(_bannerGroup.DOFade(0f, UiTheme.Instance.PanelOut))
            .OnComplete(() => { _banner.gameObject.SetActive(false); _bannerUp = false; })
            .SetLink(_banner.gameObject);
        Sfx.Play(Sound.Go, 0.8f, 0f);
    }

    private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    /// <summary>A name in its player colour (on the dark HUD, the colour itself reads well).</summary>
    private static string Nickname(PlayerRef player)
    {
        if (_spawner == null || _spawner.Object == null) _spawner = FindFirstObjectByType<BasePlayerSpawner>();
        if (_spawner != null && _spawner.Object != null && _spawner.Players.TryGet(player, out var p) && p != null && !string.IsNullOrEmpty(p.Nickname))
            return $"<color=#{Hex(p.PlayerColor)}>{p.Nickname}</color>";
        return $"Player {player.PlayerId}";
    }

    private static string PlainName(PlayerRef player)
    {
        if (_spawner != null && _spawner.Object != null && _spawner.Players.TryGet(player, out var p) && p != null && !string.IsNullOrEmpty(p.Nickname))
            return p.Nickname;
        return $"Player {player.PlayerId}";
    }
}
