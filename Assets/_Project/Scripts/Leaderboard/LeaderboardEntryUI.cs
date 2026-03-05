using DG.Tweening;
using TMPro;
using UnityEngine;

public class LeaderboardEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _rankText;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _minigameScoreText;
    [SerializeField] private TMP_Text _totalScoreText;
    [SerializeField] private TMP_Text _badgeText;       
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _contentRoot;   

    [Header("Colors")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _overallWinnerColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color _minigameWinnerColor = new Color(1f, 0.84f, 0f);

    public void Populate(LeaderboardEntry entry)
    {
        if (entry.IsEmpty)
        {
            _contentRoot.SetActive(false);
            return;
        }

        _contentRoot.SetActive(true);

        _rankText.text = $"#{entry.Rank}";
        _nameText.text = entry.PlayerName;
        _minigameScoreText.text = $"+{entry.MinigameScore}";
        _totalScoreText.text = $"{entry.TotalScore} pts";

        if (_badgeText != null)
        {
            if (entry.IsOverallWinner)
            {
                _badgeText.gameObject.SetActive(true);
                _badgeText.text = "Overall Winner";
                _nameText.color = _overallWinnerColor;
                _rankText.color = _overallWinnerColor;
            }
            else if (entry.IsMinigameWinner)
            {
                _badgeText.gameObject.SetActive(true);
                _badgeText.text = "Round Winner";
                _nameText.color = _minigameWinnerColor;
                _rankText.color = _minigameWinnerColor;
            }
            else
            {
                _badgeText.gameObject.SetActive(false);
                _nameText.color = _normalColor;
                _rankText.color = _normalColor;
            }
        }
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