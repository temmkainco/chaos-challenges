using UnityEngine;
using Zenject;

/// <summary>The 3-2-1 before a round, then GO! (A timed round's last seconds are the HUD's <see cref="ShowTimer"/>.)</summary>
public class MinigameCountdown : Countdown
{
    [Inject] private MinigameController _minigameController;

    private void OnEnable()
    {
        _minigameController.OnCountdownStarted += ShowStartCountdown;
        _minigameController.OnCountdownTick += UpdateCountdown;
        _minigameController.OnMinigameStart += PlayGo;
        _minigameController.OnMinigameEnd += HideCountdown;
    }

    private void OnDisable()
    {
        if (_minigameController == null)
            return;

        _minigameController.OnCountdownStarted -= ShowStartCountdown;
        _minigameController.OnCountdownTick -= UpdateCountdown;
        _minigameController.OnMinigameStart -= PlayGo;
        _minigameController.OnMinigameEnd -= HideCountdown;
    }

    private void PlayGo()
    {
        Sfx.Play(Sound.Go, 1f, 0f);
        ShowGo();
    }

    /// <summary>The 3-2-1 before the round: big, in the middle.</summary>
    private void ShowStartCountdown() => ShowCountdown();
}
