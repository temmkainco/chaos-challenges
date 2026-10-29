using UnityEngine;
using Zenject;

/// <summary>Start countdown, and the final seconds of a time-limited round.</summary>
public class MinigameCountdown : Countdown
{
    [Inject] private MinigameController _minigameController;

    private void OnEnable()
    {
        _minigameController.OnCountdownStarted += ShowCountdown;
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

        _minigameController.OnCountdownStarted -= ShowCountdown;
        _minigameController.OnCountdownTick -= UpdateCountdown;
        _minigameController.OnMinigameStart -= HideCountdown;
        _minigameController.OnMinigameStart -= PlayGo;
        _minigameController.OnTimeLimitTick -= OnTimeLimitTick;
        _minigameController.OnMinigameEnd -= HideCountdown;
    }

    private static void PlayGo() => Sfx.Play(Sound.Go, 1f, 0f);

    private void OnTimeLimitTick(int seconds)
    {
        if (!_countdownContainer.activeSelf)
            ShowCountdown();

        UpdateCountdown(seconds);
    }
}
