using Networking;
using TMPro;
using UnityEngine;
using Zenject;

/// <summary>Lobby screen HUD: room code and how to start; the host also gets the game pack picker (G).</summary>
public class LobbyHud : MonoBehaviour
{
    private const string GUEST_HELP = "Everyone in? Press <b>E</b> on the big red button to get ready.\n" +
                                      "<size=80%><b>L</b>: tonight's games. Hold <b>T</b> to emote, hold <b>Tab</b> for players.</size>";
    private const string HOST_HELP = "Everyone in? Press <b>E</b> on the big red button to get ready.\n" +
                                     "<size=80%><b>L</b>: tonight's games (pick them). Hold <b>T</b> to emote. Hold <b>Tab</b>: players, kick, start now.</size>";

    [SerializeField] private TMP_Text _code;
    [SerializeField] private TMP_Text _help;

    [Inject] private INetworkRunner _networkRunner;

    private string _last;

    private void Awake()
    {
        // Host-only game pack picker on its own overlay canvas.
        var go = new GameObject("GamePackCanvas", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35;
        var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        go.AddComponent<GamePackPanel>();
        PlayerColorPicker.Create(go.transform);
    }

    private void Update()
    {
        bool showHelp = GameSettings.ShowHints;
        var helpPanel = _help.transform.parent.gameObject;
        if (helpPanel.activeSelf != showHelp)
            helpPanel.SetActive(showHelp);
        string help = GamePackPanel.IsHost ? HOST_HELP : GUEST_HELP;
        if (help != _last)
        {
            _last = help;
            _help.text = help;
        }

        string code = _networkRunner?.CurrentLobbyCode;
        _code.text = string.IsNullOrEmpty(code) ? "" : $"<size=55%>ROOM CODE</size>\n<color=#E8590C>{code}</color>";
    }
}
