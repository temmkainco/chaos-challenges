using System;
using UnityEngine;

/// <summary>Player settings (audio, controls, display), saved in PlayerPrefs and applied immediately.</summary>
public static class GameSettings
{
    public static event Action Changed;

    public static float MasterVolume { get => Get("set.master", 0.65f); set => Set("set.master", Mathf.Clamp01(value)); }
    public static float MusicVolume { get => Get("set.music", 0.45f); set => Set("set.music", Mathf.Clamp01(value)); }
    public static float SfxVolume { get => Get("set.sfx", 0.5f); set => Set("set.sfx", Mathf.Clamp01(value)); }
    /// <summary>Camera speed multiplier (0.2 - 3).</summary>
    public static float MouseSensitivity { get => Get("set.sens", 1f); set => Set("set.sens", Mathf.Clamp(value, 0.2f, 3f)); }
    public static bool InvertY { get => Get("set.invert", 0f) > 0.5f; set => Set("set.invert", value ? 1f : 0f); }
    public static bool Fullscreen { get => Get("set.fullscreen", 1f) > 0.5f; set => Set("set.fullscreen", value ? 1f : 0f); }
    /// <summary>Controls hints and help lines on the HUD and in the lobby.</summary>
    public static bool ShowHints { get => Get("set.hints", 1f) > 0.5f; set => Set("set.hints", value ? 1f : 0f); }
    public static bool VSync { get => Get("set.vsync", 1f) > 0.5f; set => Set("set.vsync", value ? 1f : 0f); }
    public static int Quality { get => (int)Get("set.quality", QualitySettings.GetQualityLevel()); set => Set("set.quality", Mathf.Clamp(value, 0, QualitySettings.names.Length - 1)); }

    /// <summary>Index into <see cref="Screen.resolutions"/>; -1 keeps the current one.</summary>
    public static int Resolution { get => (int)Get("set.resolution", -1f); set => Set("set.resolution", value); }

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
        QualitySettings.SetQualityLevel(Quality, true);
        QualitySettings.vSyncCount = VSync ? 1 : 0;

#if !UNITY_EDITOR
        var resolutions = Screen.resolutions;
        var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (Resolution >= 0 && Resolution < resolutions.Length)
            Screen.SetResolution(resolutions[Resolution].width, resolutions[Resolution].height, mode);
        else if (Screen.fullScreenMode != mode)
            Screen.fullScreenMode = mode;
#endif
    }

    private static float Get(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);

    private static void Set(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        Apply();
        Changed?.Invoke();
    }
}
