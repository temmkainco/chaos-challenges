using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Lobby: the player colours as a row of swatches in a kit card, top left. Yours has a Paper ring and its name is
/// written out; colours other players have are dimmed and marked with their initial (never colour alone). C / RB
/// switches to the next free colour, or click a free swatch while the cursor is out. Your last pick is remembered
/// and asked for again when you join a lobby. Colours are kept by <see cref="NetworkGameManager"/> (unique per
/// player, for the whole session).
/// </summary>
public class PlayerColorPicker : MonoBehaviour
{
    private const string PREF = "PlayerColor";
    private const float STEP = 64f;

    private Image[] _swatches;
    private GameObject[] _rings;
    private TextMeshProUGUI[] _marks;
    private TextMeshProUGUI _name, _cap;
    private CanvasGroup _group;
    private bool _askedForPreferred;

    public static PlayerColorPicker Create(Transform parent)
    {
        int count = NetworkGameManager.PlayerColors.Length;
        var theme = UiTheme.Instance;
        var box = Ui.Box(parent, "YourColour", UiTheme.Tone.Studio);
        var rt = Ui.Place((RectTransform)box.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(count * STEP + 40f, 148f));
        var picker = rt.gameObject.AddComponent<PlayerColorPicker>();
        picker._group = rt.gameObject.AddComponent<CanvasGroup>();

        var label = Ui.Text(rt, "YOUR COLOUR", UiTheme.TextRole.Label, UiTheme.Tone.Haze, TextAlignmentOptions.Left);
        label.GetComponent<ThemedText>().SingleLine = true;
        label.GetComponent<ThemedText>().Apply();
        float labelWidth = label.GetPreferredValues(label.text).x;
        Ui.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(labelWidth + 4f, 44f));
        picker._name = Ui.Text(rt, "", UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        picker._name.fontStyle = FontStyles.Bold;
        Ui.Place(picker._name.rectTransform, new Vector2(0f, 1f), new Vector2(40f + labelWidth, -10f), new Vector2(rt.sizeDelta.x - 140f - labelWidth, 44f));

        // The key that switches colour, as a cap (follows keyboard / gamepad).
        var cap = Ui.Box(rt, "Cap", UiTheme.Tone.Paper, 10f);
        cap.ShadowDepth = theme.ShadowChip;
        cap.Apply();
        Ui.Place((RectTransform)cap.transform, new Vector2(1f, 1f), new Vector2(-20f, -14f), new Vector2(64f, 40f));
        picker._cap = Ui.Text(cap.transform, "C", UiTheme.TextRole.Label, UiTheme.Tone.Ink);
        picker._cap.GetComponent<ThemedText>().ShrinkToFit = false;
        Ui.Stretch(picker._cap.rectTransform);

        picker._swatches = new Image[count];
        picker._rings = new GameObject[count];
        picker._marks = new TextMeshProUGUI[count];
        for (int i = 0; i < count; i++)
        {
            var position = new Vector2(20f + STEP * 0.5f + i * STEP, -96f);
            var ring = Ui.Circle(rt, "Yours " + i, 62f, theme.Paper);
            Ui.Place(ring.rectTransform, new Vector2(0f, 1f), position + new Vector2(-31f, 31f), new Vector2(62f, 62f));
            picker._rings[i] = ring.gameObject;
            var outline = Ui.Circle(rt, "Outline " + i, 52f, theme.Ink);
            Ui.Place(outline.rectTransform, new Vector2(0f, 1f), position + new Vector2(-26f, 26f), new Vector2(52f, 52f));
            var swatch = Ui.Circle(outline.transform, "Colour " + i, 44f, NetworkGameManager.PlayerColors[i]);
            swatch.raycastTarget = true;
            int index = i;
            var button = swatch.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => picker.Request(index));
            picker._swatches[i] = swatch;
            picker._marks[i] = Ui.Text(swatch.transform, "", UiTheme.TextRole.Label, UiTheme.Tone.Paper);
            picker._marks[i].GetComponent<ThemedText>().ShrinkToFit = false;
            Ui.Stretch(picker._marks[i].rectTransform);
        }
        return picker;
    }

    private void Update()
    {
        var match = NetworkGameManager.Instance;
        bool visible = match != null && match.Object != null && match.Object.IsValid && match.Phase == MatchPhase.Lobby;
        _group.alpha = visible ? 1f : 0f;
        _group.blocksRaycasts = visible;
        if (!visible) return;

        var me = match.Runner.LocalPlayer;
        int mine = match.ColorIndexOf(me);

        // Ask once per lobby for the colour picked last time (the host keeps it if nobody else has it).
        if (!_askedForPreferred && mine >= 0)
        {
            _askedForPreferred = true;
            int preferred = PlayerPrefs.GetInt(PREF, -1);
            if (preferred >= 0 && preferred != mine) match.RPC_RequestColor((byte)preferred);
        }

        if (!PauseMenu.IsOpen && !GamePackPanel.IsOpen && UiInput.Pressed(Key.C, GamepadButton.RightShoulder))
            Request(NextFree(match, mine));

        var theme = UiTheme.Instance;
        for (int i = 0; i < _swatches.Length; i++)
        {
            var owner = match.OwnerOfColor(i);
            bool yours = i == mine;
            bool taken = owner != Fusion.PlayerRef.None && !yours;
            _rings[i].SetActive(yours);
            var c = NetworkGameManager.PlayerColors[i];
            _swatches[i].color = taken ? Color.Lerp(c, theme.Ink, 0.6f) : c;
            _marks[i].text = taken ? Initial(owner) : "";
        }
        _name.text = mine >= 0 ? NetworkGameManager.PlayerColorNames[mine] : "";
        _cap.text = PromptBar.UsingGamepad ? "RB" : "C";
    }

    private static int NextFree(NetworkGameManager match, int mine)
    {
        int count = NetworkGameManager.PlayerColors.Length;
        for (int step = 1; step < count; step++)
        {
            int index = ((mine < 0 ? 0 : mine) + step) % count;
            if (match.OwnerOfColor(index) == Fusion.PlayerRef.None) return index;
        }
        return mine;
    }

    private void Request(int index)
    {
        var match = NetworkGameManager.Instance;
        if (match == null || index < 0 || match.OwnerOfColor(index) != Fusion.PlayerRef.None) return;
        PlayerPrefs.SetInt(PREF, index);
        match.RPC_RequestColor((byte)index);
        Sfx.Play(Sound.Click);
    }

    private static string Initial(Fusion.PlayerRef owner)
    {
        var spawner = FindFirstObjectByType<BasePlayerSpawner>();
        if (spawner != null && spawner.Object != null && spawner.Object.IsValid
            && spawner.Players.TryGet(owner, out var player) && player != null && !string.IsNullOrEmpty(player.Nickname))
            return player.Nickname.Substring(0, 1).ToUpperInvariant();
        return "•";
    }
}
