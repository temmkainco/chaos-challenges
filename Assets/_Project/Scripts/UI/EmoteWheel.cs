using DG.Tweening;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Hold T to open the emote wheel, flick the mouse toward an emote and release T to play it (or press 1-8 while it is
/// open). The camera stops turning while the wheel is up. Lives for the whole session (DontDestroyOnLoad) and works
/// in the lobby and every minigame.
/// </summary>
public class EmoteWheel : MonoBehaviour
{
    private const int SLOTS = 9;
    private const float RADIUS = 210f;

    private CanvasGroup _group;
    private readonly Image[] _slots = new Image[SLOTS];
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
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = new GameObject("Wheel", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        _group = root.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        var kit = UiKit.Instance;

        var hub = Image(root.transform, kit != null ? kit.Circle : null, UiKit.Cream, Vector2.zero, 150f);
        Label(hub.transform, "EMOTE", 30f);
        for (int i = 0; i < SLOTS; i++)
        {
            float angle = 90f - i * (360f / SLOTS);
            var pos = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * RADIUS;
            _slots[i] = Image(root.transform, kit != null ? kit.Circle : null, UiKit.Lavender, pos, 130f);
            Label(_slots[i].transform, $"<size=70%>{i + 1}</size>\n{EmotePlayer.Names[i + 1]}", 28f);
        }
    }

    private static Image Image(Transform parent, Sprite sprite, Color color, Vector2 pos, float size)
    {
        var go = new GameObject("Slot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Label(Transform parent, string text, float size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<TextMeshProUGUI>();
        var kit = UiKit.Instance;
        if (kit != null) t.font = kit.Font;
        t.text = text;
        t.fontSize = size;
        t.color = UiKit.Ink;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.lineSpacing = -20f;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var player = Player.Local;
        bool canEmote = keyboard != null && player != null && player.Object != null && player.Object.IsValid && !PauseMenu.IsOpen;
        bool held = canEmote && keyboard.tKey.isPressed;

        if (held && !_open) Open(player);
        if (!_open) return;

        if (held)
        {
            if (Mouse.current != null)
                _pointer = Vector2.ClampMagnitude(_pointer + Mouse.current.delta.ReadValue(), 160f);
            int selected = _pointer.magnitude > 40f
                ? Mathf.RoundToInt(Mathf.Repeat(90f - Mathf.Atan2(_pointer.y, _pointer.x) * Mathf.Rad2Deg, 360f) / (360f / SLOTS)) % SLOTS
                : -1;
            for (int i = 0; i < SLOTS && keyboard != null; i++)
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) { Play(player, i); Close(); return; }
            if (selected != _selected)
            {
                _selected = selected;
                for (int i = 0; i < SLOTS; i++)
                {
                    _slots[i].color = i == selected ? UiKit.Yellow : UiKit.Lavender;
                    _slots[i].transform.DOScale(i == selected ? 1.15f : 1f, 0.12f).SetUpdate(true);
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
        foreach (var slot in _slots) { slot.color = UiKit.Lavender; slot.transform.localScale = Vector3.one; }
        _group.DOKill();
        _group.DOFade(1f, 0.12f).SetUpdate(true);
        _group.transform.localScale = Vector3.one * 0.85f;
        _group.transform.DOScale(1f, 0.15f).SetEase(Ease.OutBack).SetUpdate(true);
        _cameraInput = player.Camera != null ? player.Camera.GetComponentInChildren<CinemachineInputAxisController>(true) : null;
        if (_cameraInput != null) _cameraInput.enabled = false;
    }

    private void Close()
    {
        _open = false;
        _group.DOKill();
        _group.DOFade(0f, 0.1f).SetUpdate(true);
        if (_cameraInput != null) _cameraInput.enabled = true;
        _cameraInput = null;
    }

    private static void Play(Player player, int slot)
    {
        player.RPC_Emote((byte)(slot + 1));
        Sfx.Play(Sound.Pop, 0.8f, 0.1f);
    }
}

