using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// Arena display: whose turn it is and the turn countdown (turn-based minigames), plus the minigame's status line.
/// Reads replicated state every frame, so a late-loading client never misses the first turn.
/// </summary>
public class TurnDisplay : MonoBehaviour
{
    private const int URGENT_SECONDS = 3;

    [SerializeField] private BasePlayerSpawner _basePlayerSpawner;
    [SerializeField] private MinigameController _controller;
    [SerializeField] private TMP_Text _text;

    private string _lastText;

    private void Update()
    {
        // Scenes built from another minigame may still point at the old controller: use the scene's current one.
        if (_controller == null)
            _controller = MinigameController.Current;
        if (_controller == null || _controller.Object == null || !_controller.Object.IsValid)
            return;

        if (!_controller.IsGameActive)
        {
            SetText("");
            return;
        }

        string text = _controller is TurnBasedMinigameController turnBased ? TurnLine(turnBased) : null;
        string status = _controller.GetStatusText();
        if (!string.IsNullOrEmpty(status))
            text = text == null ? status : $"{text}\n<size=60%>{status}</size>";

        SetText(text ?? "");
    }

    private string TurnLine(TurnBasedMinigameController controller)
    {
        var currentPlayer = controller.CurrentTurn;
        if (currentPlayer == PlayerRef.None)
            return null;

        bool isLocal = currentPlayer == controller.Runner.LocalPlayer;
        string text = isLocal ? "<color=#FFD54A>Your turn!</color>" : $"{GetNickname(currentPlayer)}'s turn";
        if (controller.HasTurnTimer)
        {
            int seconds = Mathf.CeilToInt(controller.TurnTimeRemaining);
            text += seconds <= URGENT_SECONDS ? $"  <color=#FF5252>{seconds}s</color>" : $"  {seconds}s";
        }
        return text;
    }

    public string GetNickname(PlayerRef player)
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
