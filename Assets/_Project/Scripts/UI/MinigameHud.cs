using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// Minimal screen HUD shared by every minigame. The name, round and rules live on the intermission TV, so during
/// play the HUD only shows what changes: a small chip with the time left and the minigame's one status line
/// (hidden when both are empty), a controls hint for the first seconds, and a big banner that pops in and gets out of
/// the way for "YOUR TURN!" and for the announcer's lines (<see cref="MinigameController.Announcement"/>). Reads replicated state every frame.
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
    private TMP_Text _banner;
    private float _playingTime;
    private PlayerRef _lastTurn = PlayerRef.None;
    private MinigameController _shownFor;
    private int _lastAnnouncement;

    private void Awake()
    {
        if (_title != null)
            _title.gameObject.SetActive(false);
        _timer.fontSize = Mathf.Min(_timer.fontSize, 56f);
        _chip = Group(_timer.transform.parent);
        _hintGroup = Group(_hint.transform.parent);
        _banner = CreateBanner();
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
        }

        if (controller.AnnouncementId != _lastAnnouncement)
        {
            _lastAnnouncement = controller.AnnouncementId;
            PopBanner(controller.Announcement.ToString());
        }

        var definition = controller.Definition;
        if (controller.IsGameActive)
            _playingTime += Time.deltaTime;

        string timer = "";
        if (controller.IsGameActive && controller.IsTimeLimited)
        {
            int seconds = Mathf.CeilToInt(controller.TimeRemaining);
            timer = $"{seconds / 60}:{seconds % 60:00}";
            if (seconds <= URGENT_SECONDS) timer = $"<color=#E03131>{timer}</color>";
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
        Set(_status, status, ref _lastStatus);
        _status.gameObject.SetActive(status.Length > 0);
        _timer.gameObject.SetActive(timer.Length > 0);
        _chip.alpha = Mathf.MoveTowards(_chip.alpha, timer.Length + status.Length > 0 ? 1f : 0f, Time.deltaTime * 5f);

        if (_hint.text.Length == 0 && definition != null && definition.Rules != null && definition.Rules.Length > 0)
            _hint.text = definition.Rules[0];
        bool showHint = _playingTime < HINT_SECONDS;
        _hintGroup.alpha = Mathf.MoveTowards(_hintGroup.alpha, showHint ? 1f : 0f, Time.deltaTime * 2f);
    }

    private void PopBanner(string text)
    {
        _banner.text = text;
        var t = _banner.transform;
        t.DOKill();
        _banner.gameObject.SetActive(true);
        t.localScale = Vector3.zero;
        DOTween.Sequence()
            .Append(t.DOScale(1.1f, 0.25f).SetEase(Ease.OutBack))
            .Append(t.DOScale(1f, 0.1f))
            .AppendInterval(1.1f)
            .Append(t.DOScale(0f, 0.2f).SetEase(Ease.InBack))
            .OnComplete(() => _banner.gameObject.SetActive(false))
            .SetLink(_banner.gameObject);
        Sfx.Play(Sound.Go, 0.8f, 0f);
    }

    private static string Nickname(PlayerRef player)
    {
        var spawner = FindFirstObjectByType<BasePlayerSpawner>();
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
