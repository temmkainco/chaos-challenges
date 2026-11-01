using DG.Tweening;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Hold T (gamepad: d-pad up) to open the emote wheel, point at an emote with the mouse or the right stick and let go
/// to play it (or press 1-9 while it is open). The camera stops turning while the wheel is up. Built with the UI kit;
/// lives for the whole session (DontDestroyOnLoad) and works in the lobby and every minigame.
/// </summary>
public class EmoteWheel : MonoBehaviour
{
    private const int SLOTS = 9;
    private const float RADIUS = 230f, SLOT = 132f;

    private CanvasGroup _group;
    private readonly Image[] _slots = new Image[SLOTS];
    private readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[SLOTS];
    private Vector2 _pointer;
    private int _selected = -1;
    private bool _open;
    private CinemachineInputAxisController _cameraInput;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("EmoteWheel");
        DontDestroyOnLoad(go);
        go.AddComponent<EmoteWheel>();
    }

    private void Awake()
    {
        var safe = Ui.Canvas("EmoteWheel", 200, transform);
        var theme = UiTheme.Instance;
        var root = Ui.Place(Ui.Rect(safe, "Wheel"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 700f));
        _group = root.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        var hubRing = Ui.Circle(root, "HubRing", 176f, theme.Ink);
        var hub = Ui.Circle(hubRing.transform, "Hub", 160f, theme.Studio);
        var hubLabel = Ui.Text(hub.transform, "EMOTE", UiTheme.TextRole.Label, UiTheme.Tone.Haze);
        Ui.Stretch(hubLabel.rectTransform);
        for (int i = 0; i < SLOTS; i++)
        {
            float angle = 90f - i * (360f / SLOTS);
            var pos = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * RADIUS;
            var ring = Ui.Circle(root, "Slot " + (i + 1), SLOT + 12f, theme.Ink);
            ring.rectTransform.anchoredPosition = pos;
            _slots[i] = Ui.Circle(ring.transform, "Disc", SLOT, theme.Riser);
            _labels[i] = Ui.Text(_slots[i].transform, $"<size=80%>{i + 1}</size>\n{EmotePlayer.Names[i + 1]}", UiTheme.TextRole.Label, UiTheme.Tone.Paper);
            _labels[i].GetComponent<ThemedText>().Spacing = 0f;
            _labels[i].GetComponent<ThemedText>().Apply();
            _labels[i].lineSpacing = -18f;
            Ui.Stretch(_labels[i].rectTransform, 10f);
        }
    }

    private static bool Held => UiInput.Held(Key.T, GamepadButton.DpadUp);

    private void Update()
    {
        var keyboard = Keyboard.current;
        var player = Player.Local;
        bool canEmote = player != null && player.Object != null && player.Object.IsValid && !PauseMenu.IsOpen && UiModal.OpenCount == 0;
        bool held = canEmote && Held;

        if (held && !_open) Open(player);
        if (!_open) return;

        if (held)
        {
            if (Mouse.current != null)
                _pointer = Vector2.ClampMagnitude(_pointer + Mouse.current.delta.ReadValue(), 160f);
            var pad = Gamepad.current;
            if (pad != null && pad.rightStick.ReadValue().sqrMagnitude > 0.25f)
                _pointer = pad.rightStick.ReadValue() * 160f;
            int selected = _pointer.magnitude > 40f
                ? Mathf.RoundToInt(Mathf.Repeat(90f - Mathf.Atan2(_pointer.y, _pointer.x) * Mathf.Rad2Deg, 360f) / (360f / SLOTS)) % SLOTS
                : -1;
            for (int i = 0; i < SLOTS && keyboard != null; i++)
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) { Play(player, i); Close(); return; }
            if (selected != _selected)
            {
                _selected = selected;
                var theme = UiTheme.Instance;
                for (int i = 0; i < SLOTS; i++)
                {
                    _slots[i].color = i == selected ? theme.Accent : theme.Riser;
                    _labels[i].color = i == selected ? theme.Ink : theme.Paper;
                    _slots[i].transform.parent.DOScale(i == selected ? 1.12f : 1f, theme.Feedback).SetUpdate(true);
                }
            }
            return;
        }

        if (_selected >= 0 && player != null) Play(player, _selected);
        Close();
    }

    private void Open(Player player)
    {
        _open = true;
        _pointer = Vector2.zero;
        _selected = -1;
        var theme = UiTheme.Instance;
        for (int i = 0; i < SLOTS; i++) { _slots[i].color = theme.Riser; _labels[i].color = theme.Paper; _slots[i].transform.parent.localScale = Vector3.one; }
        UiMotion.PanelIn((RectTransform)_group.transform, _group);
        _cameraInput = player.Camera != null ? player.Camera.GetComponentInChildren<CinemachineInputAxisController>(true) : null;
        if (_cameraInput != null) _cameraInput.enabled = false;
    }

    private void Close()
    {
        _open = false;
        UiMotion.PanelOut((RectTransform)_group.transform, _group);
        if (_cameraInput != null) _cameraInput.enabled = true;
        _cameraInput = null;
    }

    private static void Play(Player player, int slot)
    {
        player.RPC_Emote((byte)(slot + 1));
        Sfx.Play(Sound.Pop, 0.8f, 0.1f);
    }
}

