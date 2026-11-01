using System;
using DG.Tweening;
using Networking;
using Platform;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// The main menu, built with the UI kit: the show's logo top left over a dark fade (the living room stays in view)
/// and one column of views under it — the four main buttons, then Host, Join and Settings in the same spot (the
/// logo shrinks out of their way, nothing covers it). Customize takes the whole screen with its turning preview.
/// Keyboard, mouse and gamepad all work; Back (Esc / B) steps back a view, and on the main view asks to quit.
/// Networking stays in <see cref="MainMenuController"/>.
/// </summary>
public class MainMenuView : MonoBehaviour
{
    private enum View { Main, Host, Join, Settings, Customize }

    [SerializeField] private PlayerLook _previewPrefab;

    [Inject] private INetworkRunner _networkRunner;

    private MainMenuController _controller;
    private IPlatformService _platformService;

    private RectTransform _canvas, _safe, _logo;
    private CanvasGroup _logoGroup;
    private RectTransform[] _views;
    private Vector2[] _rest;
    private GameObject[] _focus;
    private View _view;
    private PromptBar _prompts;
    private GameObject _returnFocus;

    private int _maxPlayers = SessionSettings.MAX_PLAYERS;
    private bool _isPublic = true;
    private TMP_InputField _code;
    private UiButton _joinButton;
    private ColorPickerPopup _picker;
    private GameObject _pickerRow;
    private UiStepper[] _lookRows;
    private SettingsView _settings;

    [Inject]
    public void Construct(MainMenuController controller, IPlatformService platformService)
    {
        _controller = controller;
        _platformService = platformService;
    }

    private void OnEnable()
    {
        _platformService.Init();
        if (_canvas == null) Build();
        ShowView(View.Main, true);
        LocalLook.Changed += RefreshLook;

        // The menu can become active before or after the disconnect is reported, so handle both.
        _networkRunner.Disconnected += OnDisconnected;
        ShowDisconnectReason();
    }

    private void OnDisable()
    {
        LocalLook.Changed -= RefreshLook;
        if (_networkRunner != null)
            _networkRunner.Disconnected -= OnDisconnected;
    }

    private void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    #region Build

    private void Build()
    {
        _safe = Ui.Canvas("Main Menu", 10);
        _canvas = (RectTransform)_safe.parent;
        BuildFade();
        BuildLogo();
        _views = new RectTransform[5];
        _rest = new Vector2[5];
        _focus = new GameObject[5];
        BuildMain();
        BuildHost();
        BuildJoin();
        BuildSettings();
        BuildCustomize();
    }

    /// <summary>Ink fading out to the right, under the logo and the views.</summary>
    private void BuildFade()
    {
        var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MenuFade" };
        var ink = UiTheme.Instance.Ink;
        for (int x = 0; x < 256; x++)
        {
            float t = x / 255f;
            float a = t < 0.55f ? Mathf.Lerp(0.92f, 0.72f, t / 0.55f) : Mathf.Lerp(0.72f, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));
            texture.SetPixel(x, 0, new Color(ink.r, ink.g, ink.b, a));
        }
        texture.Apply(false);
        var fade = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
        fade.transform.SetParent(_canvas, false);
        fade.transform.SetAsFirstSibling();
        fade.texture = texture;
        fade.raycastTarget = false;
        var rt = fade.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0.62f, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>The show logo (<see cref="ShowLogo"/>), top left.</summary>
    private void BuildLogo()
    {
        var logo = ShowLogo.Create(_safe, "ON AIR");
        _logo = Ui.Place(logo.Rect, new Vector2(0f, 1f), new Vector2(0f, -16f), logo.Rect.sizeDelta);
        _logoGroup = _logo.gameObject.AddComponent<CanvasGroup>();
        _logoGroup.blocksRaycasts = false;
    }

    /// <summary>A view root: a column under the logo (or the whole safe area), hidden until shown.</summary>
    private RectTransform ViewRoot(View view, string name, float width, float top)
    {
        var root = Ui.Rect(_safe, name);
        root.gameObject.SetActive(false);
        Ui.Place(root, new Vector2(0f, 1f), new Vector2(0f, top), new Vector2(width, 0f));
        Ui.Stack<VerticalLayoutGroup>(root, 2, TextAnchor.UpperLeft);
        root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        root.gameObject.AddComponent<CanvasGroup>();
        _views[(int)view] = root;
        _rest[(int)view] = root.anchoredPosition;
        return root;
    }

    /// <summary>A sub-view's heading: its title on the left, Back on the right.</summary>
    private UiButton Header(RectTransform parent, string title, float width)
    {
        var row = Ui.Rect(parent, "Header");
        Ui.Size(row, width, 88f);
        var text = Ui.Text(row, title, UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper, TextAlignmentOptions.Left, true);
        Ui.Place(text.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(width - 260f, 88f));
        var back = Ui.Button(row, "BACK", UiButton.Variant.Quiet, "back", () => ShowView(View.Main));
        Ui.Place((RectTransform)back.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(230f, 76f));
        return back;
    }

    private void BuildMain()
    {
        var root = ViewRoot(View.Main, "Main", 560f, -400f);
        UiButton Big(string label, UiButton.Variant variant, string icon, Action click)
        {
            var b = Ui.Button(root, label, variant, icon, () => click());
            Ui.Size(b, 560f, 96f);
            return b;
        }
        var host = Big("HOST A SHOW", UiButton.Variant.Primary, "play", () => ShowView(View.Host));
        var join = Big("JOIN", UiButton.Variant.Secondary, "join", () => ShowView(View.Join));
        var customize = Big("CUSTOMIZE", UiButton.Variant.Secondary, "person", () => ShowView(View.Customize));
        var settings = Big("SETTINGS", UiButton.Variant.Secondary, "gear", () => ShowView(View.Settings));
        _focus[(int)View.Main] = host.gameObject;

        // Quit sits apart, bottom left, and asks first.
        var quit = Ui.Button(_safe, "QUIT", UiButton.Variant.Quiet, "cross", ConfirmQuit);
        Ui.Place((RectTransform)quit.transform, new Vector2(0f, 0f), Vector2.zero, new Vector2(230f, 76f));
        _mainOnly = new[] { quit.gameObject, null };
        TMP_InputField nameField = null;

        // Only the dev build (no Steam) lets you type a name.
        if (_platformService is LocalPlatformService)
        {
            var tag = Ui.Rect(_safe, "Name");
            Ui.Place(tag, new Vector2(1f, 1f), Vector2.zero, new Vector2(440f, 130f));
            Ui.Stack<VerticalLayoutGroup>(tag, 1, TextAnchor.UpperRight);
            var label = Ui.Text(tag, "YOUR NAME", UiTheme.TextRole.Label, UiTheme.Tone.Haze, TextAlignmentOptions.Right);
            Ui.Size(label, 440f, 36f);
            var name = nameField = UiControls.TextField(tag, "Name", 16, false, 440f);
            name.text = _platformService.GetPlayerName();
            name.onEndEdit.AddListener(value => PlayerPrefs.SetString("LocalPlayerName", value));
            _mainOnly[1] = tag.gameObject;
        }
        UiFocus.Chain(new Selectable[] { nameField }, new Selectable[] { host }, new Selectable[] { join }, new Selectable[] { customize },
            new Selectable[] { settings }, new Selectable[] { quit });
    }

    private GameObject[] _mainOnly;

    private void BuildHost()
    {
        const float width = 860f;
        var root = ViewRoot(View.Host, "Host", width, -230f);
        var back = Header(root, "HOST A SHOW", width);
        var rows = Ui.Rect(root, "Rows");
        Ui.Stack<VerticalLayoutGroup>(rows, 2, TextAnchor.UpperLeft);
        var visible = UiControls.Toggle(rows, "Anyone can join", _isPublic);
        visible.onValueChanged.AddListener(v => _isPublic = v);
        var players = UiStepper.Create(rows, "Players", () => $"Up to {_maxPlayers}",
            d => _maxPlayers = Mathf.Clamp(_maxPlayers + d, SessionSettings.MIN_PLAYERS, SessionSettings.MAX_PLAYERS), width);
        var rounds = UiStepper.Create(rows, "Rounds", () => MatchSettings.Rounds.ToString(), d => MatchSettings.Rounds += d, width);
        var length = UiStepper.Create(rows, "Round length", () => MatchSettings.RoundLength.ToString(),
            d => MatchSettings.RoundLength = (MatchSettings.Length)(((int)MatchSettings.RoundLength + d + 3) % 3), width);
        var chaos = UiStepper.Create(rows, "Chaos", () => MatchSettings.Chaos.ToString(),
            d => MatchSettings.Chaos = (ChaosLevel)(((int)MatchSettings.Chaos + d + 3) % 3), width);
        var shoving = UiControls.Toggle(rows, "Shoving", MatchSettings.Shoving);
        shoving.onValueChanged.AddListener(v => MatchSettings.Shoving = v);

        var go = Ui.Button(root, "GO LIVE", UiButton.Variant.Primary, "play", Host);
        Ui.Size(go, 560f, 88f);
        _focus[(int)View.Host] = go.gameObject;
        UiFocus.Chain(new Selectable[] { back }, new Selectable[] { visible }, new Selectable[] { players }, new Selectable[] { rounds },
            new Selectable[] { length }, new Selectable[] { chaos }, new Selectable[] { shoving }, new Selectable[] { go });
    }

    private void BuildJoin()
    {
        const float width = 860f;
        var root = ViewRoot(View.Join, "Join", width, -230f);
        var back = Header(root, "JOIN A SHOW", width);
        var label = Ui.Text(root, "Room code", UiTheme.TextRole.Body, UiTheme.Tone.Haze, TextAlignmentOptions.Left);
        Ui.Size(label, width, 44f);
        var row = Ui.Rect(root, "Code");
        Ui.Stack<HorizontalLayoutGroup>(row, 3, TextAnchor.MiddleLeft);
        _code = UiControls.TextField(row, "ABC123", 6, true, 420f);
        _code.onValidateInput += (_, _, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
        _joinButton = Ui.Button(row, "JOIN", UiButton.Variant.Primary, "join", JoinByCode);
        Ui.Size(_joinButton, 300f, 88f);
        _joinButton.interactable = false;
        _code.onValueChanged.AddListener(v => _joinButton.interactable = v.Length is >= 2 and <= 6);
        _code.onSubmit.AddListener(_ => { if (_joinButton.interactable) JoinByCode(); });

        var spacer = Ui.Rect(root, "Gap");
        Ui.Size(spacer, width, 24f);
        var any = Ui.Button(root, "JOIN ANY OPEN SHOW", UiButton.Variant.Secondary, "play", JoinRandom);
        Ui.Size(any, 560f, 88f);
        _focus[(int)View.Join] = _code.gameObject;
        UiFocus.Chain(new Selectable[] { back }, new Selectable[] { _code, _joinButton }, new Selectable[] { any });
    }

    private void BuildSettings()
    {
        const float width = 860f;
        var root = ViewRoot(View.Settings, "Settings", width, -230f);
        var back = Header(root, "SETTINGS", width);
        _settings = SettingsView.Create(root, width);
        _settings.Above = back;
    }

    /// <summary>Customize: rows on the left (colours open the picker), the turning preview on the right.</summary>
    private void BuildCustomize()
    {
        const float width = 860f;
        var screen = Ui.Rect(_safe, "Customize");
        screen.gameObject.SetActive(false);
        Ui.Stretch(screen);
        screen.gameObject.AddComponent<CanvasGroup>();
        _views[(int)View.Customize] = screen;

        var column = Ui.Place(Ui.Rect(screen, "Column"), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 0f));
        Ui.Stack<VerticalLayoutGroup>(column, 3, TextAnchor.UpperLeft);
        column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var back = Header(column, "CUSTOMIZE", width);

        var catalog = CosmeticsCatalog.Instance;
        var rows = Ui.Rect(column, "Rows");
        Ui.Size(rows, width, 7 * 72f + 6 * UiTheme.Instance.Gap(2));
        Ui.Stack<VerticalLayoutGroup>(rows, 2, TextAnchor.UpperLeft);
        _lookRows = new UiStepper[7];
        int i = 0;
        foreach (var part in new[] { CosmeticsCatalog.Part.Body, CosmeticsCatalog.Part.Hands, CosmeticsCatalog.Part.Head })
        {
            var p = part;
            var row = UiStepper.Create(rows, p.ToString(), () => LocalLook.NameOf(LocalLook.GetColor(p)), d => LocalLook.StepColor(p, d), width);
            var swatch = row.AddSwatch();
            row.Pressed += () => OpenPicker(p, row.gameObject);
            row.Refreshed = () => swatch.color = LocalLook.GetColor(p);
            _lookRows[i++] = row;
        }
        foreach (var slot in new[] { CosmeticsCatalog.Slot.Hat, CosmeticsCatalog.Slot.Face, CosmeticsCatalog.Slot.Neck, CosmeticsCatalog.Slot.Back })
        {
            var s = slot;
            _lookRows[i++] = UiStepper.Create(rows, s.ToString(),
                () => catalog != null ? catalog.GetItem(s, LocalLook.GetItem(s)).Name : "-", d => LocalLook.StepItem(s, d), width);
        }
        float rowsHeight = 7 * 72f + 6 * UiTheme.Instance.Gap(2);
        _picker = ColorPickerPopup.Create(rows, new Rect(-width / 2f, -rowsHeight / 2f, width, rowsHeight));
        _picker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        var buttons = Ui.Rect(column, "Buttons");
        Ui.Stack<HorizontalLayoutGroup>(buttons, 3, TextAnchor.MiddleLeft);
        var random = Ui.Button(buttons, "RANDOM", UiButton.Variant.Secondary, "star", LocalLook.Randomize);
        Ui.Size(random, 300f, 88f);
        var done = Ui.Button(buttons, "DONE", UiButton.Variant.Primary, "tick", () => ShowView(View.Main));
        Ui.Size(done, 300f, 88f);
        _focus[(int)View.Customize] = _lookRows[0].gameObject;
        var order = new System.Collections.Generic.List<Selectable[]> { new Selectable[] { back } };
        foreach (var row in _lookRows) order.Add(new Selectable[] { row });
        order.Add(new Selectable[] { random, done });
        UiFocus.Chain(order.ToArray());

        var frame = Ui.Box(screen, "Preview", UiTheme.Tone.Riser);
        Ui.Place((RectTransform)frame.transform, new Vector2(1f, 0.5f), new Vector2(-40f, 24f), new Vector2(600f, 800f));
        var image = new GameObject("Model", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(frame.transform, false);
        Ui.Stretch(image.rectTransform, 12f);
        CustomizePreview.Create(image, _previewPrefab);
        RefreshLook();
    }

    #endregion

    #region Views

    private void ShowView(View view) => ShowView(view, false);

    private void ShowView(View view, bool instant)
    {
        var events = EventSystem.current;
        if (view != View.Main && _view == View.Main && events != null) _returnFocus = events.currentSelectedGameObject;
        if (_picker != null) _picker.Close();
        _view = view;
        for (int i = 0; i < _views.Length; i++) _views[i].gameObject.SetActive(i == (int)view);
        foreach (var go in _mainOnly) if (go != null) go.SetActive(view == View.Main);

        var root = _views[(int)view];
        var group = root.GetComponent<CanvasGroup>();
        if (instant) { root.anchoredPosition = _rest[(int)view]; group.alpha = 1f; }
        else UiMotion.SlideIn(root, _rest[(int)view], new Vector2(-80f, 0f), group);

        // The logo: full size on the main view, small over the other columns, gone for customize.
        float scale = view == View.Main ? 1f : 0.55f;
        float alpha = view == View.Customize ? 0f : 1f;
        float time = instant || UiTheme.ReduceMotion ? 0f : UiTheme.Instance.PanelIn;
        _logo.DOKill();
        _logo.DOScale(scale, time).SetEase(Ease.OutCubic).SetUpdate(true);
        _logoGroup.DOKill();
        _logoGroup.DOFade(alpha, time).SetUpdate(true);

        if (_prompts != null) Destroy(_prompts.gameObject);
        _prompts = PromptBar.Create(_safe, (PromptBar.Action.Confirm, "Select"), (PromptBar.Action.Back, view == View.Main ? "Quit" : "Back"));

        if (view == View.Join) { _code.text = string.Empty; _joinButton.interactable = false; }
        if (view == View.Customize) RefreshLook();
        var focus = view == View.Main && _returnFocus != null ? _returnFocus : _focus[(int)view];
        if (UiModal.OpenCount > 0) { }   // a modal (an error after a failed join) keeps focus
        else if (view == View.Settings) _settings.Focus();
        else if (events != null && focus != null) events.SetSelectedGameObject(focus);
    }

    private void Update()
    {
        if (UiModal.OpenCount > 0) return;
        if (_pickerRow != null && !_picker.gameObject.activeSelf)
        {
            EventSystem.current?.SetSelectedGameObject(_pickerRow);
            _pickerRow = null;
        }
        UiFocus.Keep(_focus[(int)_view] != null ? _focus[(int)_view] : _views[(int)_view].GetComponentInChildren<Selectable>()?.gameObject);

        if (!UiInput.Back()) return;
        if (_picker != null && _picker.gameObject.activeSelf) _picker.Close();
        else if (_view == View.Main) ConfirmQuit();
        else ShowView(View.Main);
    }

    private void OpenPicker(CosmeticsCatalog.Part part, GameObject row)
    {
        _picker.Open(part);
        _pickerRow = row;
        var first = _picker.GetComponentInChildren<UiButton>();   // OK, not the hex field
        if (first != null) EventSystem.current?.SetSelectedGameObject(first.gameObject);
    }

    private void RefreshLook()
    {
        if (_lookRows == null) return;
        foreach (var row in _lookRows) row.Refresh();
    }

    #endregion

    #region Actions

    private void ConfirmQuit()
    {
        UiModal.Show("QUIT?", null, null,
            ("STAY", UiButton.Variant.Secondary, null),
            ("QUIT", UiButton.Variant.Danger, _controller.OnExitButtonClicked));
    }

    // Static: a failed join reloads the menu scene, and these notices outlive it (the new menu sees them too).
    private static UiModal _busy, _notice;

    private static UiModal Busy(string title)
    {
        _busy = UiModal.Show(title, null, null);
        _busy.Cancelable = false;
        var dots = Ui.Text(_busy.Content, "• • •", UiTheme.TextRole.Title, UiTheme.Tone.Accent);
        Ui.Size(dots, 200f, 56f);
        if (!UiTheme.ReduceMotion)
            dots.DOFade(0.2f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(dots.gameObject);
        DontDestroyOnLoad(_busy.gameObject);
        return _busy;
    }

    /// <summary>One error notice at a time: a newer one replaces it (a retried join reports more than once).</summary>
    private static void Error(string title, string text)
    {
        if (_notice != null) _notice.Close();
        _notice = UiModal.Show(title, text, null, ("OK", UiButton.Variant.Primary, null));
        DontDestroyOnLoad(_notice.gameObject);
    }

    private async void Host()
    {
        var busy = Busy("OPENING THE STUDIO");
        try
        {
            string code = await _controller.OnHostLobbyButtonClicked(_isPublic, _maxPlayers);
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("Failed to host lobby.");
                Error("NO SIGNAL", _controller.LastError ?? "Could not create a lobby.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error hosting lobby: {e.Message}");
            Error("NO SIGNAL", "Could not create a lobby. Check your connection.");
        }
        finally
        {
            if (busy != null) busy.Close();
            if (_busy == busy) _busy = null;
        }
    }

    private async void JoinByCode()
    {
        string code = _code.text.Trim().ToUpperInvariant();
        var busy = Busy("TUNING IN");
        try
        {
            if (!await _controller.OnJoinByCodeButtonClicked(code))
            {
                Debug.LogWarning("Failed to join lobby.");
                Error("NO SIGNAL", _controller.LastError ?? "Could not join that lobby.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error joining lobby: {e.Message}");
            Error("NO SIGNAL", "Could not join that lobby. Check your connection.");
        }
        finally
        {
            if (busy != null) busy.Close();
            if (_busy == busy) _busy = null;
        }
    }

    private async void JoinRandom()
    {
        var busy = Busy("TUNING IN");
        try
        {
            if (!await _controller.OnJoinRandomButtonClicked())
            {
                Debug.LogWarning("Failed to join random lobby.");
                Error("NO SIGNAL", _controller.LastError ?? "Could not find a game to join.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error joining random lobby: {e.Message}");
            Error("NO SIGNAL", "Could not join a game. Check your connection.");
        }
        finally
        {
            if (busy != null) busy.Close();
            if (_busy == busy) _busy = null;
        }
    }

    private void OnDisconnected(string reason) => ShowDisconnectReason();

    private void ShowDisconnectReason()
    {
        string reason = _networkRunner.ConsumeDisconnectReason();
        if (reason == null) return;
        Debug.LogWarning($"Returned to menu: {reason}");
        if (_busy == null) Error("OFF AIR", reason);   // during a host / join, its own result says what went wrong
    }

    #endregion
}
