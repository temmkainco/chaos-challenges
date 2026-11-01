using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Results, built with the UI kit. After a round: a stamped ROUND OVER! over the arena and this round's places
/// (player chip, place, the round's points; match totals stay secret). The final reveal: the camera turns to the
/// stage, the screen dims around a spotlight, the broadcast scoreboard fills from last place to first, then WINNER!
/// stamps in over the winner's chip with confetti. A countdown says when the show moves on.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _focusedCamera;

    [Header("Animation")]
    [SerializeField, Tooltip("Final reveal: delay between places, revealed from last to first")]
    private float _finalRevealStagger = 1.2f;

    private const float BOARD_WIDTH = 600f, ROUND_ROW = 76f, FINAL_ROW = 88f;

    private LeaderboardController _controller;
    private RectTransform _canvas, _safe, _content;
    private CanvasGroup _group;
    private TextMeshProUGUI _countdown;
    private float _timer;
    private bool _isCounting, _isFinal;
    private Sequence _reveal;
    private GameObject _spotlight;

    private void Start()
    {
        _safe = Ui.Canvas("Results", 70);
        _canvas = (RectTransform)_safe.parent;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_canvas.gameObject, gameObject.scene);
        _group = _safe.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        _controller = FindFirstObjectByType<LeaderboardController>();
        if (_controller != null) Subscribe();
        else StartCoroutine(InitializeWhenReady());
    }

    private System.Collections.IEnumerator InitializeWhenReady()
    {
        yield return new WaitUntil(() => FindFirstObjectByType<LeaderboardController>() != null);
        _controller = FindFirstObjectByType<LeaderboardController>();
        Subscribe();
    }

    private void Subscribe()
    {
        _controller.OnLeaderboardShown += Show;
        _controller.OnLeaderboardHidden += Hide;
    }

    private void Update()
    {
        if (!_isCounting || _countdown == null) return;
        _timer = Mathf.Max(0f, _timer - Time.deltaTime);
        _countdown.text = _isFinal ? $"Back to the lobby in {Mathf.CeilToInt(_timer)}" : $"Next round in {Mathf.CeilToInt(_timer)}";
    }

    private void Show(List<LeaderboardEntry> entries, bool isFinal)
    {
        _isFinal = isFinal;
        _reveal?.Kill();
        if (_content != null) Destroy(_content.gameObject);
        if (_spotlight != null) Destroy(_spotlight);
        _content = Ui.Rect(_safe, isFinal ? "Final" : "RoundOver");
        Ui.Stretch(_content);
        Sfx.Play(isFinal ? Sound.Fanfare : Sound.RoundOver, 1f, 0f);

        entries = entries.Where(e => !e.IsEmpty).ToList();
        if (isFinal) BuildFinal(entries);
        else BuildRound(entries);

        _countdown = Ui.Text(_content, "", UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Center);
        _countdown.fontStyle = FontStyles.Bold;
        Ui.Place(_countdown.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(900f, 48f));
        _countdown.gameObject.SetActive(isFinal);

        _group.DOKill();
        _group.DOFade(1f, UiTheme.Instance.PanelIn).SetUpdate(true).SetLink(_group.gameObject);
        _group.blocksRaycasts = isFinal;
        if (isFinal && _focusedCamera != null) _focusedCamera.Priority = 20;
        _timer = _controller.RemainingTime;
        _isCounting = true;
    }

    /// <summary>ROUND OVER! and this round's places under it (no totals: they stay secret until the finale).</summary>
    private void BuildRound(List<LeaderboardEntry> entries)
    {
        var title = Ui.Text(_content, "ROUND OVER!", UiTheme.TextRole.DisplayXL, UiTheme.Tone.Accent, TextAlignmentOptions.Center, true);
        Ui.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1400f, 170f));
        title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);
        UiMotion.Stamp(title.rectTransform, title.gameObject.AddComponent<CanvasGroup>());

        var list = Ui.Place(Ui.Rect(_content, "Places"), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(BOARD_WIDTH, 0f));
        list.pivot = new Vector2(0.5f, 1f);
        Ui.Stack<VerticalLayoutGroup>(list, 2, TextAnchor.UpperCenter);
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for (int i = 0; i < entries.Count; i++)
        {
            var row = Row(list, entries[i], ROUND_ROW, entries[i].Rank == 1, $"+{entries[i].MinigameScore}");
            Pop(row, 0.35f + i * 0.15f);
        }
    }

    /// <summary>The broadcast scoreboard (right), filled last place first, then WINNER! over the winner's chip.</summary>
    private void BuildFinal(List<LeaderboardEntry> entries)
    {
        // On the canvas itself, so the dimming reaches the screen edges (not just the safe area).
        _spotlight = Spotlight.Show(_canvas, new Vector2(-260f, -40f), 380f);

        var board = Ui.Place(Ui.Rect(_content, "Board"), new Vector2(1f, 1f), new Vector2(0f, -150f), new Vector2(BOARD_WIDTH, 0f));
        Ui.Stack<VerticalLayoutGroup>(board, 2, TextAnchor.UpperLeft);
        board.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var heading = Ui.Text(board, "FINAL SCORES", UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper, TextAlignmentOptions.Left, true);
        Ui.Size(heading, BOARD_WIDTH, 80f);
        var rows = new List<RectTransform>();
        foreach (var entry in entries)
            rows.Add(Row(board, entry, FINAL_ROW, entry.IsOverallWinner, entry.TotalScore.ToString()));
        foreach (var row in rows) row.GetComponent<CanvasGroup>().alpha = 0f;

        var winners = entries.Where(e => e.IsOverallWinner).ToList();
        float winnerAt = (entries.Count - 1) * _finalRevealStagger + 0.8f;
        _reveal = DOTween.Sequence().SetUpdate(true).SetLink(_content.gameObject);
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var row = rows[i];
            _reveal.InsertCallback((entries.Count - 1 - i) * _finalRevealStagger + 0.4f, () => Pop(row, 0f));
        }
        _reveal.InsertCallback(winnerAt, () => ShowWinner(winners));
    }

    private void ShowWinner(List<LeaderboardEntry> winners)
    {
        var title = Ui.Text(_content, winners.Count > 1 ? "WINNERS!" : "WINNER!", UiTheme.TextRole.DisplayXL, UiTheme.Tone.Accent, TextAlignmentOptions.Center, true);
        Ui.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(-260f, -10f), new Vector2(1000f, 180f));
        title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);
        UiMotion.Stamp(title.rectTransform, title.gameObject.AddComponent<CanvasGroup>());

        var chips = Ui.Place(Ui.Rect(_content, "Winner"), new Vector2(0.5f, 0f), new Vector2(-260f, 110f), new Vector2(1000f, 84f));
        Ui.Stack<HorizontalLayoutGroup>(chips, 3, TextAnchor.MiddleCenter);
        foreach (var winner in winners)
        {
            var chip = PlayerChip.Create(chips, 84f);
            Ui.Size(chip, 440f, 84f);
            chip.Set(winner.Number, winner.Color, winner.PlayerName, "star");
        }
        Confetti.Burst(_content, new Vector2(-260f, 0f), 90);
        Sfx.Play(Sound.Fanfare, 0.8f, 0f);
        _countdown.gameObject.SetActive(true);
    }

    /// <summary>One scoreboard row: place block (Accent for the top), the player's chip disc and name, a value.</summary>
    private static RectTransform Row(Transform parent, LeaderboardEntry entry, float height, bool top, string value)
    {
        var theme = UiTheme.Instance;
        var rt = Ui.Rect(parent, entry.PlayerName);
        Ui.Size(rt, BOARD_WIDTH, height);
        rt.gameObject.AddComponent<CanvasGroup>();
        if (top)
        {
            var ring = Ui.Box(rt, "TopRing", UiTheme.Tone.Accent, -1f, false);
            ring.HasOutline = false;
            Ui.Stretch((RectTransform)ring.transform, -(theme.FocusRing + theme.Outline) + 2f);
        }
        var box = Ui.Box(rt, "Box", UiTheme.Tone.Studio);
        Ui.Stretch((RectTransform)box.transform);
        var rank = Ui.Box(box.transform, "Place", top ? UiTheme.Tone.Accent : UiTheme.Tone.Riser, -1f, false);
        Ui.Place((RectTransform)rank.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(height, height));
        var rankText = Ui.Text(rank.transform, entry.Rank.ToString(), UiTheme.TextRole.DisplayM, top ? UiTheme.Tone.Ink : UiTheme.Tone.Paper);
        Ui.Stretch(rankText.rectTransform);
        var discRing = Ui.Circle(box.transform, "DiscRing", 52f, theme.Ink);
        Ui.Place(discRing.rectTransform, new Vector2(0f, 0.5f), new Vector2(height + 16f, 0f), new Vector2(52f, 52f));
        var disc = Ui.Circle(discRing.transform, "Disc", 42f, entry.Color);
        var number = Ui.Text(disc.transform, entry.Number > 0 ? entry.Number.ToString() : "", UiTheme.TextRole.Label, UiTheme.Tone.Ink);
        number.GetComponent<ThemedText>().ShrinkToFit = false;
        number.GetComponent<ThemedText>().Spacing = 0f;
        number.GetComponent<ThemedText>().Apply();
        Ui.Stretch(number.rectTransform);
        var name = Ui.Text(box.transform, entry.PlayerName, UiTheme.TextRole.Title, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        name.GetComponent<ThemedText>().SingleLine = true;
        name.GetComponent<ThemedText>().Apply();
        name.overflowMode = TextOverflowModes.Ellipsis;
        var n = name.rectTransform;
        n.anchorMin = Vector2.zero; n.anchorMax = Vector2.one; n.offsetMin = new Vector2(height + 84f, 0f); n.offsetMax = new Vector2(-150f, 0f);
        var points = Ui.Text(box.transform, value, UiTheme.TextRole.DisplayM, top ? UiTheme.Tone.Accent : UiTheme.Tone.Paper, TextAlignmentOptions.Right);
        Ui.Place(points.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(140f, height));
        return rt;
    }

    private static void Pop(RectTransform row, float delay)
    {
        var group = row.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        DOVirtual.DelayedCall(delay, () =>
        {
            if (row == null) return;
            UiMotion.Stamp(row, group);
            Sfx.Play(Sound.Pop, 0.6f, 0f);
        }, true).SetLink(row.gameObject);
    }

    private void Hide()
    {
        _isCounting = false;
        _reveal?.Kill();
        if (_spotlight != null) Destroy(_spotlight);
        if (_group == null) return;
        _group.DOKill();
        _group.DOFade(0f, UiTheme.Instance.PanelOut).SetUpdate(true).SetLink(_group.gameObject)
            .OnComplete(() => _group.blocksRaycasts = false);
    }

    private void OnDestroy()
    {
        _reveal?.Kill();
        if (_canvas != null) Destroy(_canvas.gameObject);
        if (_controller == null) return;
        _controller.OnLeaderboardShown -= Show;
        _controller.OnLeaderboardHidden -= Hide;
    }
}
