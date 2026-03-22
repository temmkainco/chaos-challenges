using Fusion;
using TMPro;
using UnityEngine;

public class TurnDisplay : MonoBehaviour
{
    [SerializeField] private BasePlayerSpawner _basePlayerSpawner;
    [SerializeField] private TurnBasedMinigameController _controller;
    [SerializeField] private TMP_Text _text;

    private PlayerRef _pendingPlayer;
    private Player _pendingPlayerObject;

    private void OnEnable() => _controller.OnTurnChanged += OnTurnChanged;

    private void OnDisable()
    {
        _controller.OnTurnChanged -= OnTurnChanged;
        UnsubscribeFromNickname();
    }

    private void OnTurnChanged(PlayerRef player, bool isFirst)
    {
        UnsubscribeFromNickname();

        _pendingPlayer = player;

        if (_basePlayerSpawner.Players.TryGet(player, out var playerObject) && playerObject != null)
        {
            _pendingPlayerObject = playerObject;
            _pendingPlayerObject.OnNicknameUpdated += OnNicknameUpdated;
        }

        RefreshText();
    }

    private void OnNicknameUpdated(string _)
    {
        RefreshText();
    }

    private void RefreshText()
    {
        bool isLocal = _pendingPlayer == _controller.Runner.LocalPlayer;

        string nickname = string.IsNullOrEmpty(_pendingPlayerObject?.Nickname)
            ? null
            : _pendingPlayerObject.Nickname;

        if (nickname != null)
        {
            _text.text = isLocal ? $"Your turn, {nickname}!" : $"{nickname}'s turn";
            UnsubscribeFromNickname(); 
        }
        else
        {
            _text.text = isLocal ? "Your turn!" : $"Player {_pendingPlayer}'s turn";
        }
    }

    private void UnsubscribeFromNickname()
    {
        if (_pendingPlayerObject != null)
        {
            _pendingPlayerObject.OnNicknameUpdated -= OnNicknameUpdated;
            _pendingPlayerObject = null;
        }
    }
}