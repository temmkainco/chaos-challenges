using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// Minimal screen HUD shared by every minigame, styled like a TV broadcast: a compact "LIVE" chip in the top-right
/// corner with the time left and the minigame's one status line (hidden when both are empty), a round-name splash
/// that slides in at the countdown and animates out, a controls hint for the first seconds, and a big banner for
/// "YOUR TURN!" and the announcer's lines (<see cref="MinigameController.Announcement"/>). Reads replicated state every frame.
/// </summary>
public class MinigameHud : MonoBehaviour
{
    private const int URGENT_SECONDS = 10;
    private const float HINT_SECONDS = 7f;

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _timer;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private TMP_Text _hint;

    private string _lastTimer, _lastStatus;
    private CanvasGroup _chip, _hintGroup;
    private TMP_Text _banner, _splash, _callout;
    private readonly System.Collections.Generic.Queue<string> _bannerQueue = new();
    private float _playingTime;
    private PlayerRef _lastTurn = PlayerRef.None;
    private MinigameController _shownFor;
    private int _lastAnnouncement;
    private int _lastTimerSeconds = -1;
    private bool _chipVisible;
    private float _nextStatus;
    private static BasePlayerSpawner _spawner;

    private void Awake()
    {
        if (_title != null)
            _title.gameObject.SetActive(false);
        _timer.fontSize = 40f;
        _status.fontSize = 26f;
        // Broadcast-style corner chip instead of a top-centre panel.
        var chipRect = (RectTransform)_timer.transform.parent;
        chipRect.anchorMin = chipRect.anchorMax = chipRect.pivot = new Vector2(1f, 1f);
        chipRect.anchoredPosition = new Vector2(-24f, -20f);
        chipRect.sizeDelta = new Vector2(360f, chipRect.sizeDelta.y);
        _chip = Group(_timer.transform.parent);
        _hintGroup = Group(_hint.transform.parent);
        _banner = CreateBanner();
        CreateRulesCard();
        gameObject.AddComponent<ChaosEffects>();
        _callout = CreateBanner();
        _callout.name = "Callout";
        _callout.fontSize = 84f;
        ((RectTransform)_callout.transform).anchorMin = ((RectTransform)_callout.transform).anchorMax = new Vector2(0.5f, 0.86f);
        _splash = CreateBanner();
        _splash.name = "RoundSplash";
        _splash.fontSize = 96f;
        var splashRect = (RectTransform)_splash.transform;
        splashRect.anchorMin = splashRect.anchorMax = new Vector2(0.5f, 0.8f);
    }

    /// <summary>The rules card gets its own overlay canvas: this HUD is hidden during the intro.</summary>
    private void CreateRulesCard()
    {
        var go = new GameObject("RulesCardCanvas", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        go.AddComponent<RulesCard>();
    }

    private static CanvasGroup Group(Transform t)
    {
        if (!t.TryGetComponent<CanvasGroup>(out var group))
            group = t.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private TMP_Text CreateBanner()
    {
        var go = new GameObject("TurnBanner", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.68f);
        rt.sizeDelta = new Vector2(1400f, 200f);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = _timer.font;
        text.fontSize = 120f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.79f, 0.24f);
        text.outlineWidth = 0.28f;
        text.outlineColor = new Color32(46, 26, 71, 255);
        text.raycastTarget = false;
        text.text = "";
        go.SetActive(false);
        return text;
    }

    private void Update()
    {
        var controller = MinigameController.Current;
        bool visible = controller != null && controller.Object != null && controller.Object.IsValid
                       && (controller.Phase == MinigamePhase.Countdown || controller.Phase == MinigamePhase.Playing);
        _group.alpha = Mathf.MoveTowards(_group.alpha, visible ? 1f : 0f, Time.deltaTime * 4f);
        if (!visible)
            return;

        if (controller != _shownFor)
        {
            _shownFor = controller;
            _playingTime = 0f;
            _lastTurn = PlayerRef.None;
            _hint.text = "";
            _lastAnnouncement = controller.AnnouncementId;
            ShowRoundSplash(controller);
        }

        if (controller.AnnouncementId != _lastAnnouncement)
        {
            _lastAnnouncement = controller.AnnouncementId;
            _bannerQueue.Enqueue(controller.Announcement.ToString());
        }
        // One banner at a time: chaos warnings used to wipe out Color Code's call before anyone read it.
        if (_bannerQueue.Count > 0 && !_banner.gameObject.activeSelf)
            PopBanner(_bannerQueue.Dequeue());

        // A persistent big call-out for the minigame's current instruction (Color Code's colour).
        string callout = controller.IsGameActive ? controller.Callout : null;
        bool showCallout = !string.IsNullOrEmpty(callout);
        if (_callout.gameObject.activeSelf != showCallout) _callout.gameObject.SetActive(showCallout);
        if (showCallout && _callout.text != callout) _callout.text = callout;

        var definition = controller.Definition;
        if (controller.IsGameActive)
            _playingTime += Time.deltaTime;

        // Timer text only changes once a second; the status line is refreshed four times a second.
        int timerSeconds = controller.IsGameActive && controller.IsTimeLimited ? Mathf.CeilToInt(controller.TimeRemaining) : -1;
        bool statusDue = Time.unscaledTime >= _nextStatus;
        if (timerSeconds == _lastTimerSeconds && !statusDue)
            goto Hint;
        _lastTimerSeconds = timerSeconds;
        _nextStatus = Time.unscaledTime + 0.25f;

        string timer = "";
        if (timerSeconds >= 0)
        {
            int seconds = timerSeconds;
            timer = $"{seconds / 60}:{seconds % 60:00}";
            if (seconds <= URGENT_SECONDS) timer = $"<color=#E03131>{timer}</color>";
            timer = $"<size=55%><color=#E03131><size=180%>●</size></color> LIVE</size>  {timer}";
        }
        Set(_timer, timer, ref _lastTimer);

        string status = controller.IsGameActive ? controller.GetStatusText() ?? "" : "";
        if (controller.IsGameActive && controller is TurnBasedMinigameController turnBased && turnBased.CurrentTurn != PlayerRef.None)
        {
            bool mine = turnBased.CurrentTurn == controller.Runner.LocalPlayer;
            string turn = mine ? "<color=#E8590C>Your turn</color>" : $"{Nickname(turnBased.CurrentTurn)}'s turn";
            if (turnBased.HasTurnTimer)
                turn += $"  {Mathf.CeilToInt(turnBased.TurnTimeRemaining)}s";
            status = string.IsNullOrEmpty(status) ? turn : $"{turn}\n<size=75%>{status}</size>";

            if (turnBased.CurrentTurn != _lastTurn)
            {
                _lastTurn = turnBased.CurrentTurn;
                if (mine)
                    PopBanner("YOUR TURN!");
            }
        }
        // Chaos about to start, then the active one (buff / debuff / hazard) on top of the status line, with countdowns.
        if (controller.PendingChaos != ChaosModifier.None)
        {
            string incoming = $"<color=#{ColorUtility.ToHtmlStringRGB(ChaosInfo.KindColor(controller.PendingChaos) * 0.8f)}><b>{ChaosInfo.Title(controller.PendingChaos)}</b> in {Mathf.CeilToInt(controller.PendingTimeRemaining)}...</color>";
            status = string.IsNullOrEmpty(status) ? incoming : $"{incoming}\n{status}";
        }
        else if (controller.ActiveChaos != ChaosModifier.None)
        {
            string chaos = $"<color=#{ColorUtility.ToHtmlStringRGB(ChaosInfo.KindColor(controller.ActiveChaos) * 0.8f)}><b>{ChaosInfo.Title(controller.ActiveChaos)}</b> {Mathf.CeilToInt(controller.ChaosTimeRemaining)}s</color>";
            status = string.IsNullOrEmpty(status) ? chaos : $"{chaos}\n{status}";
        }
        Set(_status, status, ref _lastStatus);
        _status.gameObject.SetActive(status.Length > 0);
        _timer.gameObject.SetActive(timer.Length > 0);
        _chipVisible = timer.Length + status.Length > 0;

    Hint:
        _chip.alpha = Mathf.MoveTowards(_chip.alpha, _chipVisible ? 1f : 0f, Time.deltaTime * 5f);
        if (_hint.text.Length == 0 && definition != null && definition.Rules != null && definition.Rules.Length > 0)
            _hint.text = definition.Rules[0];
        bool showHint = GameSettings.ShowHints && controller.IsGameActive && _playingTime < HINT_SECONDS;
        _hintGroup.alpha = Mathf.MoveTowards(_hintGroup.alpha, showHint ? 1f : 0f, Time.deltaTime * 2f);
    }

    /// <summary>"ROUND 2 · BOMB RUSH" slides in from the left at the countdown and slides away again.</summary>
    private void ShowRoundSplash(MinigameController controller)
    {
        var gameManager = NetworkGameManager.Instance;
        string name = controller.Definition != null ? controller.Definition.DisplayName.ToUpperInvariant() : "";
        string round = gameManager != null && gameManager.RoundCount > 0 ? $"<size=55%>ROUND {gameManager.RoundIndex + 1} OF {gameManager.RoundCount}</size>\n" : "";
        string map = controller.VariantName != null ? $"\n<size=45%>{controller.VariantName.ToUpperInvariant()}</size>" : "";
        _splash.text = round + name + map;
        var t = (RectTransform)_splash.transform;
        t.DOKill();
        _splash.gameObject.SetActive(true);
        t.anchoredPosition = new Vector2(-1400f, 0f);
        DOTween.Sequence()
            .Append(t.DOAnchorPosX(0f, 0.4f).SetEase(Ease.OutBack))
            .AppendInterval(1.3f)
            .Append(t.DOAnchorPosX(1400f, 0.35f).SetEase(Ease.InBack))
            .OnComplete(() => _splash.gameObject.SetActive(false))
            .SetLink(_splash.gameObject);
    }

    private void PopBanner(string text)
    {
        // Chaos (incoming and started) gets its own look and stays up longer so nobody misses it.
        bool chaos = text.Contains(" IN 3") || text.StartsWith("BUFF:") || text.StartsWith("DEBUFF:") || text.StartsWith("HAZARD:");
        _banner.fontSize = chaos ? 96f : 120f;
        _banner.color = chaos ? new Color(0.78f, 0.62f, 1f) : new Color(1f, 0.79f, 0.24f);
        float hold = chaos ? 2.2f : 1.1f;
        _banner.text = text;
        var t = _banner.transform;
        t.DOKill();
        _banner.gameObject.SetActive(true);
        t.localScale = Vector3.zero;
        DOTween.Sequence()
            .Append(t.DOScale(1.1f, 0.25f).SetEase(Ease.OutBack))
            .Append(t.DOScale(1f, 0.1f))
            .AppendInterval(hold)
            .Append(t.DOScale(0f, 0.2f).SetEase(Ease.InBack))
            .OnComplete(() => _banner.gameObject.SetActive(false))
            .SetLink(_banner.gameObject);
        Sfx.Play(Sound.Go, 0.8f, 0f);
    }

    private static string Nickname(PlayerRef player)
    {
        if (_spawner == null) _spawner = FindFirstObjectByType<BasePlayerSpawner>();
        var spawner = _spawner;
        if (spawner != null && spawner.Object != null && spawner.Players.TryGet(player, out var p) && p != null && !string.IsNullOrEmpty(p.Nickname))
            return $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(p.OutfitColor, new Color(0.18f, 0.1f, 0.28f), 0.35f))}>{p.Nickname}</color>";
        return $"Player {player.PlayerId}";
    }

    private static void Set(TMP_Text text, string value, ref string last)
    {
        if (value == last) return;
        last = value;
        text.text = value;
    }
}
