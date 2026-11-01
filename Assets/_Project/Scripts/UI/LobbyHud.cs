using Networking;
using TMPro;
using UnityEngine;
using Zenject;

/// <summary>
/// Lobby HUD, built with the UI kit: your colour (top left), the room code (top right, click to copy) and the
/// button prompts (bottom right, hidden with Settings › Show hints off). Tonight's games
/// (<see cref="GamePackPanel"/>) opens over it on its own canvas.
/// </summary>
public class LobbyHud : MonoBehaviour
{
    [Inject] private INetworkRunner _networkRunner;

    private GameObject _codeBox;
    private TextMeshProUGUI _code;
    private PromptBar _prompts;
    private CanvasGroup _hud;

    private void Awake()
    {
        var safe = Ui.Canvas("Lobby HUD", 20, transform);
        _hud = safe.gameObject.AddComponent<CanvasGroup>();
        PlayerColorPicker.Create(safe);

        var box = Ui.Box(safe, "RoomCode", UiTheme.Tone.Studio);
        var rt = Ui.Place((RectTransform)box.transform, new Vector2(1f, 1f), Vector2.zero, new Vector2(340f, 148f));
        _codeBox = rt.gameObject;
        var label = Ui.Text(rt, "ROOM CODE", UiTheme.TextRole.Label, UiTheme.Tone.Haze);
        Ui.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(300f, 40f));
        _code = Ui.Text(rt, "", UiTheme.TextRole.DisplayM, UiTheme.Tone.Accent, TextAlignmentOptions.Center, true);
        _code.GetComponent<ThemedText>().Spacing = 6f;
        _code.GetComponent<ThemedText>().Apply();
        Ui.Place(_code.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(310f, 84f));
        var copy = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        copy.transition = UnityEngine.UI.Selectable.Transition.None;
        copy.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        copy.onClick.AddListener(CopyCode);

        _prompts = PromptBar.Create(safe, (PromptBar.Action.Interact, "Ready"), (PromptBar.Action.Games, "Games"),
            (PromptBar.Action.Emote, "Emote"), (PromptBar.Action.Players, "Players"));

        var games = Ui.Canvas("Games", 40, transform);
        games.parent.gameObject.AddComponent<GamePackPanel>();
    }

    private void CopyCode()
    {
        if (string.IsNullOrEmpty(_code.text)) return;
        GUIUtility.systemCopyBuffer = _code.text;
        UiToast.Show("Room code copied", UiTheme.Tone.Good, "tick");
    }

    private void Update()
    {
        string code = _networkRunner?.CurrentLobbyCode;
        bool hasCode = !string.IsNullOrEmpty(code);
        if (_codeBox.activeSelf != hasCode) _codeBox.SetActive(hasCode);
        if (hasCode && _code.text != code) _code.text = code;

        // The pause menu has the screen to itself.
        _hud.alpha = PauseMenu.IsOpen ? 0f : 1f;
        _hud.blocksRaycasts = !PauseMenu.IsOpen;
        bool hints = GameSettings.ShowHints && !GamePackPanel.IsOpen;
        if (_prompts.gameObject.activeSelf != hints) _prompts.gameObject.SetActive(hints);
    }
}
