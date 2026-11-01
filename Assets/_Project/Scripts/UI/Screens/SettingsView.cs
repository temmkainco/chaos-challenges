using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Settings, built with the UI kit into any parent (main menu, pause menu): Audio, Controls and Display tabs. Every
/// change is saved and applied at once (<see cref="GameSettings"/>, <see cref="UiTheme.ReduceMotion"/>).
/// </summary>
public class SettingsView : MonoBehaviour
{
    private RectTransform[] _pages;
    private UiTabBar _tabs;

    /// <summary>What Up reaches from the tabs (a Back button above the view), for keyboard / gamepad order.</summary>
    public Selectable Above
    {
        get => _above;
        set { _above = value; Show(_tabs.Index); }
    }
    private Selectable _above;

    public static SettingsView Create(Transform parent, float width = 900f)
    {
        var root = Ui.Rect(parent, "Settings");
        Ui.Size(root, width, 580f);
        var view = root.gameObject.AddComponent<SettingsView>();
        Ui.Stack<VerticalLayoutGroup>(root, 4, TextAnchor.UpperCenter);

        view._tabs = UiTabBar.Create(root, ("AUDIO", null), ("CONTROLS", null), ("DISPLAY", null));
        Ui.Size(view._tabs, width, 80f);
        view._tabs.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        var pages = Ui.Rect(root, "Pages");
        Ui.Size(pages, width, 480f);
        view._pages = new[] { Page(pages, "Audio"), Page(pages, "Controls"), Page(pages, "Display") };

        var audio = view._pages[0];
        Bind(UiControls.Slider(audio, "Master", 0f, 100f, GameSettings.MasterVolume * 100f, "{0:0}%"), v => GameSettings.MasterVolume = v / 100f);
        Bind(UiControls.Slider(audio, "Music", 0f, 100f, GameSettings.MusicVolume * 100f, "{0:0}%"), v => GameSettings.MusicVolume = v / 100f);
        Bind(UiControls.Slider(audio, "Effects", 0f, 100f, GameSettings.SfxVolume * 100f, "{0:0}%"), v => GameSettings.SfxVolume = v / 100f);

        var controls = view._pages[1];
        Bind(UiControls.Slider(controls, "Mouse speed", 0.2f, 3f, GameSettings.MouseSensitivity, "{0:0.0}×"), v => GameSettings.MouseSensitivity = v);
        UiControls.Toggle(controls, "Invert camera Y", GameSettings.InvertY).onValueChanged.AddListener(v => GameSettings.InvertY = v);
        UiControls.Toggle(controls, "Show hints", GameSettings.ShowHints).onValueChanged.AddListener(v => GameSettings.ShowHints = v);

        var display = view._pages[2];
        UiControls.Toggle(display, "Fullscreen", GameSettings.Fullscreen).onValueChanged.AddListener(v => GameSettings.Fullscreen = v);
        UiControls.Toggle(display, "VSync", GameSettings.VSync).onValueChanged.AddListener(v => GameSettings.VSync = v);
        UiStepper.Create(display, "Quality", () => QualitySettings.names[Mathf.Clamp(GameSettings.Quality, 0, QualitySettings.names.Length - 1)],
            d => GameSettings.Quality = (GameSettings.Quality + d + QualitySettings.names.Length) % QualitySettings.names.Length);
        UiStepper.Create(display, "Resolution", ResolutionName, StepResolution);
        UiControls.Toggle(display, "Reduce motion", UiTheme.ReduceMotion).onValueChanged.AddListener(v => UiTheme.ReduceMotion = v);
        var build = Ui.Text(display, "Build " + Application.version, UiTheme.TextRole.Label, UiTheme.Tone.Haze, TMPro.TextAlignmentOptions.Right);
        Ui.Size(build, width, 40f);

        view._tabs.Selected += view.Show;
        view.Show(0);
        return view;
    }

    private static RectTransform Page(RectTransform parent, string name)
    {
        var page = Ui.Rect(parent, name);
        Ui.Stretch(page);
        var column = Ui.Stack<VerticalLayoutGroup>(page, 2, TextAnchor.UpperCenter);
        column.padding = new RectOffset(0, 0, 8, 0);
        return page;
    }

    private static void Bind(Slider slider, System.Action<float> set) => slider.onValueChanged.AddListener(v => set(v));

    private static string ResolutionName()
    {
        var resolutions = Screen.resolutions;
        int index = GameSettings.Resolution;
        return index >= 0 && index < resolutions.Length ? $"{resolutions[index].width} × {resolutions[index].height}" : $"{Screen.width} × {Screen.height}";
    }

    private static void StepResolution(int delta)
    {
        var resolutions = Screen.resolutions;
        if (resolutions.Length == 0) return;
        int current = GameSettings.Resolution < 0 ? resolutions.Length - 1 : GameSettings.Resolution;
        GameSettings.Resolution = (current + delta + resolutions.Length) % resolutions.Length;
    }

    private void Show(int page)
    {
        for (int i = 0; i < _pages.Length; i++) _pages[i].gameObject.SetActive(i == page);
        var rows = new System.Collections.Generic.List<Selectable[]> { new[] { Above }, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<Selectable>(_tabs.Tabs)) };
        foreach (var control in _pages[page].GetComponentsInChildren<Selectable>())
            if (control.navigation.mode != Navigation.Mode.None) rows.Add(new[] { control });
        UiFocus.Chain(rows.ToArray());
        Focus();
    }

    /// <summary>Puts keyboard / gamepad focus on the open tab's first control.</summary>
    public void Focus()
    {
        if (EventSystem.current == null) return;
        foreach (var page in _pages)
            if (page.gameObject.activeInHierarchy)
            {
                var first = page.GetComponentInChildren<Selectable>();
                if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
    }
}
