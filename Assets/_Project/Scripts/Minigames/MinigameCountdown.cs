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
        _minigameController.OnMinigameStart -= PlayGo;
        _minigameController.OnTimeLimitTick -= OnTimeLimitTick;
        _minigameController.OnMinigameEnd -= HideCountdown;
    }

    private void PlayGo()
    {
        Sfx.Play(Sound.Go, 1f, 0f);
        ShowGo();
    }

    /// <summary>The 3-2-1 before the round: big, in the middle.</summary>
    private void ShowStartCountdown() => ShowCountdown(false);

    /// <summary>The last seconds of the round: small and up top, so the arena stays visible.</summary>
    private void OnTimeLimitTick(int seconds)
    {
        ShowCountdown(true);
        UpdateCountdown(seconds);
    }
}
