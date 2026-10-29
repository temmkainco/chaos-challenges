using Fusion;
using TMPro;
using UnityEngine;

/// <summary>Whose turn it is, the turn countdown and the minigame's status line.</summary>
public class TurnDisplay : MonoBehaviour
{
    private const int URGENT_SECONDS = 3;

    [SerializeField] private BasePlayerSpawner _basePlayerSpawner;
    [SerializeField] private TurnBasedMinigameController _controller;
    [SerializeField] private TMP_Text _text;

    private string _lastText;

    // Reads the replicated turn every frame, so a late-loading client never misses the first turn.
    private void Update()
    {
        if (_controller.Object == null || !_controller.Object.IsValid)
            return;

        var currentPlayer = _controller.CurrentTurn;
        if (!_controller.IsGameActive || currentPlayer == PlayerRef.None)
        {
            SetText("");
            return;
        }

        bool isLocal = currentPlayer == _controller.Runner.LocalPlayer;
        string name = GetNickname(currentPlayer);
        string text = isLocal ? "<color=#FFD54A>Your turn!</color>" : $"{name}'s turn";

        if (_controller.HasTurnTimer)
        {
            int seconds = Mathf.CeilToInt(_controller.TurnTimeRemaining);
            text += seconds <= URGENT_SECONDS ? $"  <color=#FF5252>{seconds}s</color>" : $"  {seconds}s";
        }

        string status = _controller.GetStatusText();
        if (!string.IsNullOrEmpty(status))
            text += $"\n<size=60%>{status}</size>";

        SetText(text);
    }

    private string GetNickname(PlayerRef player)
    {
        if (_basePlayerSpawner.Object != null && _basePlayerSpawner.Players.TryGet(player, out var playerObject)
            && playerObject != null && !string.IsNullOrEmpty(playerObject.Nickname))
        {
            return playerObject.Nickname;
        }
        return $"Player {player.PlayerId}";
    }

    private void SetText(string text)
    {
        if (text == _lastText)
            return;

        _lastText = text;
        _text.text = text;
    }
}
