using UnityEngine;
using TMPro;
using DG.Tweening;
using Zenject;

public class LobbyCountdown : Countdown
{
    [Inject] private LobbyManager _lobbyManager;

    private void OnEnable()
    {
        _lobbyManager.OnCountdownStarted += ShowCountdown;
        _lobbyManager.OnCountdownCancelled += HideCountdown;
        _lobbyManager.OnCountdownTick += UpdateCountdown;
    }

    private void OnDisable()
    {
        if (_lobbyManager == null)
            return;
        
        _lobbyManager.OnCountdownStarted -= ShowCountdown;
        _lobbyManager.OnCountdownCancelled -= HideCountdown;
        _lobbyManager.OnCountdownTick -= UpdateCountdown;
    }
}