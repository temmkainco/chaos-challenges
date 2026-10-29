using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// Screen HUD shared by every minigame: name and round, time left, the minigame's status line, whose turn it is,
/// and a controls hint at the bottom. Reads replicated state every frame.
/// </summary>
public class MinigameHud : MonoBehaviour
{
    private const int URGENT_SECONDS = 10;

    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _timer;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private TMP_Text _hint;

    private string _lastTimer, _lastStatus, _lastTitle;

    private void Update()
    {
        var controller = MinigameController.Current;
        bool visible = controller != null && controller.Object != null && controller.Object.IsValid
                       && (controller.Phase == MinigamePhase.Countdown || controller.Phase == MinigamePhase.Playing);
        _group.alpha = Mathf.MoveTowards(_group.alpha, visible ? 1f : 0f, Time.deltaTime * 4f);
        if (!visible)
            return;

        var definition = controller.Definition;
        var gameManager = NetworkGameManager.Instance;
        string title = definition != null ? definition.DisplayName.ToUpperInvariant() : "";
        if (gameManager != null && gameManager.RoundCount > 0)
            title += $"  <size=70%><color=#FFFFFFAA>ROUND {gameManager.RoundIndex + 1}/{gameManager.RoundCount}</color></size>";
        Set(_title, title, ref _lastTitle);

        string timer = "";
        if (controller.IsGameActive && controller.IsTimeLimited)
        {
            int seconds = Mathf.CeilToInt(controller.TimeRemaining);
            timer = $"{seconds / 60}:{seconds % 60:00}";
            if (seconds <= URGENT_SECONDS) timer = $"<color=#FF6A50>{timer}</color>";
        }
        Set(_timer, timer, ref _lastTimer);

        string status = controller.IsGameActive ? controller.GetStatusText() ?? "" : "Get ready...";
        if (controller.IsGameActive && controller is TurnBasedMinigameController turnBased && turnBased.CurrentTurn != PlayerRef.None)
        {
            bool mine = turnBased.CurrentTurn == controller.Runner.LocalPlayer;
            string turn = mine ? "<color=#FFD54A>YOUR TURN!</color>" : $"{Nickname(turnBased.CurrentTurn)}'s turn";
            if (turnBased.HasTurnTimer)
                turn += $"  {Mathf.CeilToInt(turnBased.TurnTimeRemaining)}s";
            status = string.IsNullOrEmpty(status) ? turn : $"{turn}\n<size=75%>{status}</size>";
        }
        Set(_status, status, ref _lastStatus);

        if (_hint.text.Length == 0 && definition != null && definition.Rules != null && definition.Rules.Length > 0)
            _hint.text = definition.Rules[0];
    }

    private static string Nickname(PlayerRef player)
    {
        var spawner = FindFirstObjectByType<BasePlayerSpawner>();
        if (spawner != null && spawner.Object != null && spawner.Players.TryGet(player, out var p) && p != null && !string.IsNullOrEmpty(p.Nickname))
            return $"<color=#{ColorUtility.ToHtmlStringRGB(p.OutfitColor)}>{p.Nickname}</color>";
        return $"Player {player.PlayerId}";
    }

    private static void Set(TMP_Text text, string value, ref string last)
    {
        if (value == last) return;
        last = value;
        text.text = value;
    }
}
