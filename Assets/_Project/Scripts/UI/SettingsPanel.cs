using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Audio, controls and display settings. Every change is saved and applied right away.</summary>
public class SettingsPanel : PanelBase
{
    [SerializeField] private Slider _master;
    [SerializeField] private Slider _music;
    [SerializeField] private Slider _sfx;
    [SerializeField] private Slider _sensitivity;
    [SerializeField] private Toggle _invertY;
    [SerializeField] private Toggle _fullscreen;
    [SerializeField] private Toggle _vsync;
    [SerializeField] private Toggle _hints;
    [SerializeField] private Button _qualityPrevious;
    [SerializeField] private Button _qualityNext;
    [SerializeField] private TMP_Text _quality;
    [SerializeField] private Button _resolutionPrevious;
    [SerializeField] private Button _resolutionNext;
    [SerializeField] private TMP_Text _resolution;
    [SerializeField] private Button _closeButton;

    private bool _refreshing;

    private void Awake()
    {
        // Set the range first: changing it clamps the value and would otherwise fire the listener.
        _sensitivity.minValue = 0.2f;
        _sensitivity.maxValue = 3f;

        _master.onValueChanged.AddListener(v => { if (!_refreshing) GameSettings.MasterVolume = v; });
        _music.onValueChanged.AddListener(v => { if (!_refreshing) GameSettings.MusicVolume = v; });
        _sfx.onValueChanged.AddListener(v => { if (!_refreshing) GameSettings.SfxVolume = v; });
        _sensitivity.onValueChanged.AddListener(v => { if (!_refreshing) GameSettings.MouseSensitivity = v; });
        _invertY.onValueChanged.AddListener(v => { if (!_refreshing) GameSettings.InvertY = v; });
        _fullscreen.onValueChanged.AddListener(v => { if (!_refreshing) GameSettings.Fullscreen = v; });
        _vsync.onValueChanged.AddListener(v => { if (!_refreshing) GameSettings.VSync = v; });
        if (_hints != null) _hints.onValueChanged.AddListener(v => { if (!_refreshing) GameSettings.ShowHints = v; });
        _qualityPrevious.onClick.AddListener(() => StepQuality(-1));
        _qualityNext.onClick.AddListener(() => StepQuality(1));
        _resolutionPrevious.onClick.AddListener(() => StepResolution(-1));
        _resolutionNext.onClick.AddListener(() => StepResolution(1));
        _closeButton.onClick.AddListener(Close);
    }

    private void OnEnable() => Refresh();

    private void StepQuality(int delta)
    {
        int count = QualitySettings.names.Length;
        GameSettings.Quality = (GameSettings.Quality + delta + count) % count;
        Refresh();
    }

    private void StepResolution(int delta)
    {
        var resolutions = Screen.resolutions;
        if (resolutions.Length == 0) return;
        int current = GameSettings.Resolution < 0 ? resolutions.Length - 1 : GameSettings.Resolution;
        GameSettings.Resolution = (current + delta + resolutions.Length) % resolutions.Length;
        Refresh();
    }

    private void Refresh()
    {
        _refreshing = true;
        _master.value = GameSettings.MasterVolume;
        _music.value = GameSettings.MusicVolume;
        _sfx.value = GameSettings.SfxVolume;
        _sensitivity.value = GameSettings.MouseSensitivity;
        _invertY.isOn = GameSettings.InvertY;
        _fullscreen.isOn = GameSettings.Fullscreen;
        if (_hints != null) _hints.isOn = GameSettings.ShowHints;
        _vsync.isOn = GameSettings.VSync;
        _quality.text = QualitySettings.names[Mathf.Clamp(GameSettings.Quality, 0, QualitySettings.names.Length - 1)];

        var resolutions = Screen.resolutions;
        int index = GameSettings.Resolution;
        _resolution.text = index >= 0 && index < resolutions.Length
            ? $"{resolutions[index].width} x {resolutions[index].height}"
            : $"{Screen.width} x {Screen.height}";
        _refreshing = false;
    }
}
