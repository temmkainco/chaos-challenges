using UnityEngine;
using Zenject;

/// <summary>Start countdown, and the final seconds of a time-limited round.</summary>
public class MinigameCountdown : Countdown
{
    [Inject] private MinigameController _minigameController;

    private void OnEnable()
    {
        _minigameController.OnCountdownStarted += ShowStartCountdown;
        _minigameController.OnCountdownTick += UpdateCountdown;
        _minigameController.OnMinigameStart += HideCountdown;
        _minigameController.OnMinigameStart += PlayGo;
        _minigameController.OnTimeLimitTick += OnTimeLimitTick;
        _minigameController.OnMinigameEnd += HideCountdown;
    }

    private void OnDisable()
    {
        if (_minigameController == null)
            return;

        _minigameController.OnCountdownStarted -= ShowStartCountdown;
        _minigameController.OnCountdownTick -= UpdateCountdown;
        _minigameController.OnMinigameStart -= HideCountdown;
        _minigameController.OnMinigameStart -= PlayGo;
        _minigameController.OnTimeLimitTick -= OnTimeLimitTick;
        _minigameController.OnMinigameEnd -= HideCountdown;
    }

    private static void PlayGo() => Sfx.Play(Sound.Go, 1f, 0f);

    private Vector2 _restPosition;
    private bool _restCaptured;

    /// <summary>The 3-2-1 before the round: big, in the middle.</summary>
    private void ShowStartCountdown()
    {
        Place(false);
        ShowCountdown();
    }

    /// <summary>The last seconds of the round: small and up top, so the arena stays visible.</summary>
    private void Place(bool corner)
    {
        var rt = (RectTransform)_countdownContainer.transform;
        if (!_restCaptured) { _restPosition = rt.anchoredPosition; _restCaptured = true; }
        rt.localScale = Vector3.one * (corner ? 0.4f : 1f);
        rt.anchoredPosition = _restPosition + (corner ? new Vector2(0f, 360f) : Vector2.zero);
    }

    private void OnTimeLimitTick(int seconds)
    {
        if (!_countdownContainer.activeSelf)
        {
            Place(true);
            ShowCountdown();
        }

        UpdateCountdown(seconds);
    }
}
