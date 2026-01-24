using UnityEngine;
using TMPro;
using DG.Tweening;
using Zenject;

public class LobbyCountdown : MonoBehaviour
{
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField] private CanvasGroup _countdownPanel;
    [SerializeField] private GameObject _countdownContainer;

    [Inject] private LobbyManager _lobbyManager;

    private void Start()
    {
        _countdownContainer.SetActive(false);

        _lobbyManager.OnCountdownStarted += ShowCountdown;
        _lobbyManager.OnCountdownCancelled += HideCountdown;
        _lobbyManager.OnCountdownTick += UpdateCountdown;
    }

    private void OnDestroy()
    {
        if (_lobbyManager != null)
        {
            _lobbyManager.OnCountdownStarted -= ShowCountdown;
            _lobbyManager.OnCountdownCancelled -= HideCountdown;
            _lobbyManager.OnCountdownTick -= UpdateCountdown;
        }
    }

    private void ShowCountdown()
    {
        _countdownContainer.SetActive(true);

        _countdownPanel.alpha = 0;
        _countdownPanel.DOFade(1f, 0.1f);
    }

    private void HideCountdown()
    {
        _countdownPanel.DOFade(0f, 0.1f).OnComplete(() => {
            _countdownContainer.SetActive(false);
        });
    }

    private void UpdateCountdown(int seconds)
    {
        _countdownText.text = seconds.ToString();

        _countdownText.transform.localScale = Vector3.one;
        _countdownText.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, 5, 0.6f);
    }
}