using Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// The pause menu while connected (Esc / Start): Resume, Settings (the shared <see cref="SettingsView"/>) and Leave
/// (asks first), built with the UI kit over a dimmed screen; the show keeps running behind it. Back steps out of
/// Settings, then closes. Also owns cursor handling for the whole game: the cursor is captured while you control a
/// character and free in menus, the intermission and results.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Inject] private INetworkRunner _networkRunner;

    private GameObject _scrim;
    private RectTransform _main, _settingsColumn;
    private SettingsView _settings;
    private UiButton _resume, _settingsButton;
    private PromptBar _prompts;
    private RectTransform _safe;

    private void Awake()
    {
        _safe = Ui.Canvas("Pause", 800, transform);
        var canvas = _safe.parent;
        var theme = UiTheme.Instance;
        var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        scrim.transform.SetParent(canvas, false);
        scrim.transform.SetAsFirstSibling();
        Ui.Stretch(scrim.rectTransform);
        scrim.color = new Color(theme.Ink.r, theme.Ink.g, theme.Ink.b, 0.78f);
        _scrim = scrim.gameObject;

        _main = Column("Paused", 560f);
        var title = Ui.Text(_main, "PAUSED", UiTheme.TextRole.DisplayL, UiTheme.Tone.Paper, TMPro.TextAlignmentOptions.Left, true);
        Ui.Size(title, 560f, 130f);
        _resume = Big(_main, "RESUME", UiButton.Variant.Primary, "play", Close);
        _settingsButton = Big(_main, "SETTINGS", UiButton.Variant.Secondary, "gear", () => ShowSettings(true));
        var leave = Big(_main, "LEAVE SHOW", UiButton.Variant.Danger, "back", ConfirmLeave);
        UiFocus.Chain(new Selectable[] { _resume }, new Selectable[] { _settingsButton }, new Selectable[] { leave });

        const float width = 860f;
        _settingsColumn = Column("Settings", width);
        var header = Ui.Rect(_settingsColumn, "Header");
        Ui.Size(header, width, 88f);
        var heading = Ui.Text(header, "SETTINGS", UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper, TMPro.TextAlignmentOptions.Left, true);
        Ui.Place(heading.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(width - 260f, 88f));
        var back = Ui.Button(header, "BACK", UiButton.Variant.Quiet, "back", () => ShowSettings(false));
        Ui.Place((RectTransform)back.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(230f, 76f));
        _settings = SettingsView.Create(_settingsColumn, width);
        _settings.Above = back;

        SetVisible(false);
    }

    private RectTransform Column(string name, float width)
    {
        var column = Ui.Rect(_safe, name);
        Ui.Place(column, new Vector2(0f, 1f), new Vector2(0f, -40f), new Vector2(width, 0f));
        Ui.Stack<VerticalLayoutGroup>(column, 3, TextAnchor.UpperLeft);
        column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        column.gameObject.AddComponent<CanvasGroup>();
        return column;
    }

    private static UiButton Big(Transform parent, string label, UiButton.Variant variant, string icon, UnityEngine.Events.UnityAction click)
    {
        var b = Ui.Button(parent, label, variant, icon, click);
        Ui.Size(b, 560f, 96f);
        return b;
    }

    private void Update()
    {
        bool connected = _networkRunner != null && _networkRunner.IsConnected;
        if (!connected && IsOpen)
            Close();

        if (IsOpen)
        {
            if (UiModal.OpenCount == 0)
            {
                UiFocus.Keep(_settingsColumn.gameObject.activeSelf ? null : _resume.gameObject);
                bool start = UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.startButton.wasPressedThisFrame;
                if (UiInput.Back() || start)
                {
                    if (_settingsColumn.gameObject.activeSelf) ShowSettings(false);
                    else Close();
                }
            }
        }
        else if (connected && UiInput.Pause && !UiInput.BackTaken && UiModal.OpenCount == 0 && !GamePackPanel.IsOpen)
            Open();

        UpdateCursor();
    }

    private void UpdateCursor()
    {
        bool messageOpen = UiModal.OpenCount > 0;
        bool playing = Player.Local != null && !IsOpen && !messageOpen && Application.isFocused && !SteamInviteListener.OverlayActive
                       && SceneManager.GetActiveScene().path != GameScenes.Intermission && !RulesCard.WantsCursor && !GamePackPanel.IsOpen && !PlayerListPanel.IsOpen;
        var lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != lockState) Cursor.lockState = lockState;
        if (Cursor.visible == playing) Cursor.visible = !playing;
    }

    private void SetVisible(bool visible)
    {
        _scrim.SetActive(visible);
        _main.gameObject.SetActive(visible);
        _settingsColumn.gameObject.SetActive(false);
        if (_prompts != null) Destroy(_prompts.gameObject);
        _prompts = visible ? PromptBar.Create(_safe, (PromptBar.Action.Confirm, "Select"), (PromptBar.Action.Back, "Resume")) : null;
    }

    private void ShowSettings(bool show)
    {
        _main.gameObject.SetActive(!show);
        _settingsColumn.gameObject.SetActive(show);
        var column = show ? _settingsColumn : _main;
        UiMotion.SlideIn(column, new Vector2(0f, -40f), new Vector2(-80f, 0f), column.GetComponent<CanvasGroup>());
        if (show) _settings.Focus();
        else EventSystem.current?.SetSelectedGameObject(_settingsButton.gameObject);
    }

    private void Open()
    {
        IsOpen = true;
        SetVisible(true);
        UiMotion.SlideIn(_main, new Vector2(0f, -40f), new Vector2(-80f, 0f), _main.GetComponent<CanvasGroup>());
        EventSystem.current?.SetSelectedGameObject(_resume.gameObject);
    }

    private void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        SetVisible(false);
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
    }

    private void ConfirmLeave()
    {
        UiModal.Show("LEAVE THE SHOW?", null, null,
            ("STAY", UiButton.Variant.Secondary, null),
            ("LEAVE", UiButton.Variant.Danger, Leave));
    }

    private void Leave()
    {
        Close();
        _networkRunner.Leave();
    }
}
