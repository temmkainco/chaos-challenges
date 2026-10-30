using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _rankText;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _minigameScoreText;
    [SerializeField] private TMP_Text _totalScoreText;
    [SerializeField] private TMP_Text _badgeText;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _contentRoot;
    [SerializeField] private Image _rankPanel;

    [Header("Colors")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _overallWinnerColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color _minigameWinnerColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color _rankPanelColor = new Color(1f, 0.54f, 0.24f);
    [SerializeField] private Color _winnerPanelColor = new Color(1f, 0.79f, 0.24f);

    public void Populate(LeaderboardEntry entry)
    {
        if (entry.IsEmpty)
        {
            _contentRoot.SetActive(false);
            return;
        }

        _contentRoot.SetActive(true);
        _nameText.text = entry.PlayerName;
        _nameText.color = _normalColor;
        SetPanel(_rankPanelColor);

        if (entry.RoundPodium)
        {
            _rankText.text = $"#{entry.Rank}";
            _minigameScoreText.text = "";
            _totalScoreText.text = entry.Rank == 1 ? "ROUND WIN!" : "";
            if (_badgeText != null)
                _badgeText.gameObject.SetActive(false);
            if (entry.Rank == 1)
                SetPanel(_winnerPanelColor);
            return;
        }

        if (entry.HideScores)
        {
            _rankText.text = "?";
            _minigameScoreText.text = "";
            _totalScoreText.text = "??? pts";
            if (_badgeText != null)
                _badgeText.gameObject.SetActive(false);
            return;
        }

        _rankText.text = $"#{entry.Rank}";
        _minigameScoreText.text = $"+{entry.MinigameScore}";
        _totalScoreText.text = $"{entry.TotalScore} pts";

        bool winner = entry.IsOverallWinner || entry.IsMinigameWinner;
        if (_badgeText != null)
        {
            _badgeText.gameObject.SetActive(winner);
            _badgeText.text = entry.IsOverallWinner ? "WINNER!" : "ROUND WINNER";
        }
        if (winner)
        {
            _nameText.color = entry.IsOverallWinner ? _overallWinnerColor : _minigameWinnerColor;
            SetPanel(_winnerPanelColor);
        }
    }

    private void SetPanel(Color color)
    {
        if (_rankPanel != null)
            _rankPanel.color = color;
    }

    public void AnimateIn(float delay)
    {
        _canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * 0.85f;

        var seq = DOTween.Sequence().SetDelay(delay);
        seq.Join(_canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutSine));
        seq.Join(transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
    }
}
