using DG.Tweening;
using TMPro;
using UnityEngine;

public class Countdown : MonoBehaviour
{
    [SerializeField] protected TMP_Text _countdownText;
    [SerializeField] protected CanvasGroup _countdownPanel;
    protected GameObject _countdownContainer;

    protected virtual void Awake()
    {
        _countdownContainer = _countdownPanel.gameObject;
        _countdownContainer.SetActive(false);
    }

    protected void ShowCountdown()
    {
        _countdownContainer.SetActive(true);

        _countdownPanel.alpha = 0;
        _countdownPanel.DOFade(1f, 0.1f);
    }

    protected virtual void HideCountdown()
    {
        _countdownPanel.DOFade(0f, 0.1f).OnComplete(() => {
            _countdownContainer.SetActive(false);
        });
    }

    protected virtual void UpdateCountdown(int seconds)
    {
        _countdownText.text = seconds.ToString();

        _countdownText.transform.localScale = Vector3.one;
        _countdownText.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 5, 0.6f);
    }
}
