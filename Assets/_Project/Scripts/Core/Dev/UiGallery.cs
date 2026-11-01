using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dev scene (Scenes/Dev/UiGallery): every UI kit component on two pages, for review and screenshots at any
/// resolution. Page 1: controls and their states; page 2: the show pieces (title card, timer, scoreboard, lower
/// third, toast, spotlight, confetti, modal). Keys: 1 / 2 switch pages, R replays page 2's animations.
/// </summary>
public class UiGallery : MonoBehaviour
{
    private RectTransform _page1, _page2, _safe;
    private ShowTimer _timer;
    private float _clock;

    private void Start()
    {
        _safe = Ui.Canvas("Gallery", 10, transform);
        BuildControls();
        BuildShow();
        ShowPage(1);
    }

    private void Update()
    {
        var keys = UnityEngine.InputSystem.Keyboard.current;
        if (keys != null && keys.digit1Key.wasPressedThisFrame) ShowPage(1);
        if (keys != null && keys.digit2Key.wasPressedThisFrame) ShowPage(2);
        if (keys != null && keys.rKey.wasPressedThisFrame) ShowPage(2);
        if (_page2 != null && _page2.gameObject.activeSelf)
        {
            _clock += Time.unscaledDeltaTime;
            _timer.Set(Mathf.Max(0f, 14f - _clock), 60f);
        }
    }

    public void ShowPage(int page)
    {
        _page1.gameObject.SetActive(page == 1);
        _page2.gameObject.SetActive(page == 2);
        if (page == 2) PlayShow();
    }

    private static void Force(UiButton button, string state)
    {
        // Show a state without a pointer (the gallery is a still): Selectable's own transition method.
        var states = typeof(Selectable).GetNestedType("SelectionState", BindingFlags.NonPublic);
        var method = typeof(UiButton).GetMethod("DoStateTransition", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(button, new[] { System.Enum.Parse(states, state), (object)true });
    }

    private void BuildControls()
    {
        _page1 = Ui.Rect(_safe, "Page1");
        Ui.Stretch(_page1);
        var title = Ui.Text(_page1, "UI KIT · CONTROLS", UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper, TMPro.TextAlignmentOptions.Left, true);
        Ui.Place(title.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(1200f, 80f));

        // Buttons in every state.
        var buttons = Ui.Rect(_page1, "Buttons");
        Ui.Place(buttons, new Vector2(0f, 1f), new Vector2(0f, -120f), new Vector2(480f, 800f));
        Ui.Stack<VerticalLayoutGroup>(buttons, 4, TextAnchor.UpperLeft);
        (string label, UiButton.Variant v, string icon, string state)[] specs =
        {
            ("HOST A SHOW", UiButton.Variant.Primary, "play", "Normal"),
            ("JOIN", UiButton.Variant.Secondary, "join", "Highlighted"),
            ("CUSTOMIZE", UiButton.Variant.Secondary, "person", "Pressed"),
            ("SETTINGS", UiButton.Variant.Secondary, "gear", "Selected"),
            ("LEAVE", UiButton.Variant.Danger, "back", "Normal"),
            ("LOCKED", UiButton.Variant.Secondary, null, "Disabled"),
        };
        foreach (var (label, v, icon, state) in specs)
        {
            var b = Ui.Button(buttons, label, v, icon);
            Ui.Size(b, 440f, 88f);
            var caption = Ui.Text(b.transform, state == "Highlighted" ? "hover" : state == "Selected" ? "focus" : state.ToLowerInvariant(),
                UiTheme.TextRole.Label, UiTheme.Tone.Ink, TMPro.TextAlignmentOptions.Left);
            Ui.Place(caption.rectTransform, new Vector2(1f, 0.5f), new Vector2(150f, 0f), new Vector2(130f, 40f));
            if (state == "Disabled") b.interactable = false;
            else Force(b, state);
        }

        // A panel with tabs, a slider and toggles, as in Settings.
        var panel = Ui.Box(_page1, "Panel", UiTheme.Tone.Studio);
        var panelRt = Ui.Place((RectTransform)panel.transform, new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(1040f, 520f));
        var column = Ui.Rect(panelRt, "Column");
        Ui.Stretch(column, 40f);
        Ui.Stack<VerticalLayoutGroup>(column, 3, TextAnchor.UpperCenter).childForceExpandWidth = false;
        var tabs = UiTabBar.Create(column, ("AUDIO", null), ("VIDEO", null), ("CONTROLS", null));
        Ui.Size(tabs, 900f, 80f);
        UiControls.Slider(column, "Music", 0f, 100f, 70f, "{0:0}%");
        UiControls.Slider(column, "Effects", 0f, 100f, 45f, "{0:0}%");
        UiControls.Toggle(column, "Show hints", true);
        UiControls.Toggle(column, "Reduce motion", false);

        // Player chips: colour + number + name + status.
        var chips = Ui.Rect(_page1, "Chips");
        Ui.Place(chips, new Vector2(1f, 0f), new Vector2(0f, 110f), new Vector2(1040f, 200f));
        var grid = chips.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(500f, 72f);
        grid.spacing = new Vector2(24f, 24f);
        var colors = NetworkGameManager.PlayerColors;
        string[] names = { "nunishnii", "Player 2", "Player 3", "a_very_long_nickname_here" };
        string[] status = { "tick", null, "cross", "star" };
        for (int i = 0; i < 4; i++) PlayerChip.Create(chips).Set(i + 1, colors[i], names[i], status[i]);

        PromptBar.Create(_page1, (PromptBar.Action.Confirm, "Select"), (PromptBar.Action.Back, "Back"), (PromptBar.Action.Games, "Games"));
    }

    private LowerThird _lower;
    private TitleCard _card;
    private ScoreboardRow[] _rows;

    private void BuildShow()
    {
        _page2 = Ui.Rect(_safe, "Page2");
        Ui.Stretch(_page2);
        _timer = ShowTimer.Create(_page2);

        var tag = Ui.Box(_page2, "RoundTag", UiTheme.Tone.Brand, 6f);
        foreach (var image in tag.GetComponentsInChildren<Image>()) image.gameObject.AddComponent<UiSkew>();
        Ui.Place((RectTransform)tag.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, 70f));
        var tagText = Ui.Text(tag.transform, "ROUND 2/5", UiTheme.TextRole.Title, UiTheme.Tone.Paper);
        Ui.Stretch(tagText.rectTransform);

        var board = Ui.Rect(_page2, "Scoreboard");
        Ui.Place(board, new Vector2(1f, 0.5f), new Vector2(-150f, -80f), new Vector2(640f, 420f));
        Ui.Stack<VerticalLayoutGroup>(board, 3, TextAnchor.UpperRight);
        var heading = Ui.Text(board, "SCORES", UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper, TMPro.TextAlignmentOptions.Left, true);
        Ui.Size(heading, 640f, 80f);
        _rows = new ScoreboardRow[3];
        for (int i = 0; i < 3; i++) { _rows[i] = ScoreboardRow.Create(board); Ui.Size(_rows[i], 640f, 88f); }

        _lower = LowerThird.Create(_page2);
        _card = TitleCard.Create(_page2);
        ((RectTransform)_card.transform).anchoredPosition = new Vector2(-260f, 160f);
        PromptBar.Create(_page2, (PromptBar.Action.Interact, "Grab"));
    }

    private void PlayShow()
    {
        _clock = 4.6f;   // inside the timer's last 10 seconds, to show the loud count
        var colors = NetworkGameManager.PlayerColors;
        _rows[0].Set(1, 1, colors[0], "nunishnii", 300, true);
        _rows[1].Set(2, 2, colors[1], "Player 2", 280, false);
        _rows[2].Set(3, 3, colors[2], "Player 3", 210, false);
        _rows[0].AddPoints(420);
        _rows[1].AddPoints(380);
        _card.Show("ROUND 2", "PRIZE GRAB", UiTheme.Tone.Paper, 30f);
        _lower.Show("GOLDEN RAIN!", "Gold boxes for 4 s", 30f);
        UiToast.Show("Player 3 joined", UiTheme.Tone.Good);
    }
}
