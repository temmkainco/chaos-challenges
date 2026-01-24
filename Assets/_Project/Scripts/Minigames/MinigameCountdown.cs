using UnityEngine;
using Zenject;

public class MinigameCountdown : Countdown
{
    [Inject] private MinigameController _minigameController;

    private void OnEnable()
    {
        _minigameController.OnCountdownStarted += ShowCountdown;
        _minigameController.OnCountdownTick += UpdateCountdown;
        _minigameController.OnMinigameStart += HideCountdown;
    }

    private void OnDisable()
    {
        if (_minigameController == null)
            return;

        _minigameController.OnCountdownStarted += ShowCountdown;
        _minigameController.OnCountdownTick += UpdateCountdown;
        _minigameController.OnMinigameStart -= HideCountdown;
    }
}
