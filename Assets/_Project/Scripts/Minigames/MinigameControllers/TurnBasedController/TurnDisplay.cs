using Fusion;
using TMPro;
using UnityEngine;
using Zenject;

public class TurnDisplay : MonoBehaviour
{
    [SerializeField] private BasePlayerSpawner _basePlayerSpawner;
    [SerializeField] private TurnBasedMinigameController _controller;
    [SerializeField] private TMP_Text _text;

    private void OnEnable() => _controller.OnTurnChanged += OnTurnChanged;
    private void OnDisable() => _controller.OnTurnChanged -= OnTurnChanged;

    private void OnTurnChanged(PlayerRef player)
    {
        bool isLocal = player == _controller.Runner.LocalPlayer;

        if (_basePlayerSpawner.Players.TryGet(player, out var playerObject))
        {
            string nickname = playerObject.Nickname;
            _text.text = isLocal ? $"Your turn, {nickname}!" : $"{nickname}'s turn";
        }
    }
}