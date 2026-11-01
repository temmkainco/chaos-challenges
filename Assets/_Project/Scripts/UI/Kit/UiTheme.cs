using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Every design token of the ParTV look (dark TV-studio show) in one asset, Resources/UiTheme: colours, fonts, the
/// type scale, spacing, corners, outlines, shadows and motion. The kit's components read it, so changing a value here
/// restyles the whole game (in the editor live: <see cref="Changed"/>). Nothing outside these colours is used.
/// </summary>
[CreateAssetMenu(menuName = "Game/UI Theme")]
public class UiTheme : ScriptableObject
{
    /// <summary>The palette by role. Player colours are separate (<see cref="NetworkGameManager.PlayerColors"/>).</summary>
    public enum Tone { Ink, Studio, Riser, Haze, Paper, Brand, Accent, Good, Warn, Bad }

    /// <summary>Text roles of the type scale (sizes in canvas units at 1920x1080).</summary>
    public enum TextRole { DisplayXL, DisplayL, DisplayM, Title, Button, Body, Label }

    [Header("Colours")]
    public Color Ink = new Color32(0x14, 0x0B, 0x2E, 0xFF);
    public Color Studio = new Color32(0x24, 0x16, 0x4D, 0xFF);
    public Color Riser = new Color32(0x36, 0x24, 0x6E, 0xFF);
    public Color Haze = new Color32(0xA9, 0x9C, 0xD6, 0xFF);
    public Color Paper = new Color32(0xFF, 0xF7, 0xEA, 0xFF);
    public Color Brand = new Color32(0xE8, 0x17, 0x5D, 0xFF);
    public Color Accent = new Color32(0xFF, 0xC2, 0x1A, 0xFF);
    public Color Good = new Color32(0x2B, 0xD6, 0x7B, 0xFF);
    public Color Warn = new Color32(0xFF, 0x8A, 0x00, 0xFF);
    public Color Bad = new Color32(0xFF, 0x3B, 0x4E, 0xFF);
    [Tooltip("Dims the world behind modals and the winner reveal")]
    [Range(0f, 1f)] public float ScrimAlpha = 0.6f;

    [Header("Fonts (Latin + Cyrillic)")]
    public TMP_FontAsset Display;
    [Tooltip("Display text drawn straight over the 3D world: the same font with a thick Ink outline")]
    public Material DisplayOutline;
    public TMP_FontAsset UiBold;
    public TMP_FontAsset UiBlack;

    [Header("Type scale (canvas units at 1920x1080)")]
    public float DisplayXL = 140f;
    public float DisplayL = 96f;
    public float DisplayM = 64f;
    public float Title = 44f;
    public float ButtonText = 38f;
    public float Body = 32f;
    [Tooltip("Nothing smaller: readable on a TV and a Steam Deck")]
    public float Label = 26f;

    [Header("Layout")]
    [Tooltip("Base of the 8-point spacing scale")] public float Space = 8f;
    public float RadiusChip = 10f;
    public float RadiusPanel = 22f;
    public float Outline = 5f;
    public float OutlineTitleCard = 7f;
    public float Shadow = 8f;
    public float ShadowChip = 5f;
    [Tooltip("Broadcast elements (lower thirds, round tags) lean this many degrees")] public float BannerSlant = 10f;
    [Tooltip("Share of the screen kept clear on every side (graphics-safe area)")] [Range(0f, 0.1f)] public float SafeArea = 0.05f;
    public float FocusRing = 7f;
    public float FocusScale = 1.04f;

    [Header("Canvas")]
    public Vector2 ReferenceResolution = new(1920f, 1080f);
    [Tooltip("Expand: the whole 1920x1080 design always fits (16:10 and 4:3 get extra height, 21:9 extra width); nothing overlaps or is cut off")]
    public UnityEngine.UI.CanvasScaler.ScreenMatchMode ScreenMatch = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
    [Tooltip("Used only when ScreenMatch is MatchWidthOrHeight: 0 = width, 1 = height")]
    [Range(0f, 1f)] public float MatchWidthOrHeight = 1f;

    [Header("Motion (seconds)")]
    public float Feedback = 0.09f;
    public float PanelIn = 0.24f;
    public float PanelOut = 0.16f;
    public float Stamp = 0.35f;
    public float Wipe = 0.4f;
    public float LowerThirdHold = 3f;
    [Tooltip("Overshoot of pop-ins (ease-out-back)")] public float Overshoot = 1.15f;
    [Tooltip("The timer stays a quiet bar until this many seconds are left")] public float TimerLoudSeconds = 10f;

    /// <summary>Raised when the asset is edited (editor), so components restyle live.</summary>
    public static event Action Changed;

    private static UiTheme _instance;

    public static UiTheme Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<UiTheme>("UiTheme");
            if (_instance == null) _instance = CreateInstance<UiTheme>();
            return _instance;
        }
    }

    /// <summary>Reduce motion (settings): no overshoot, wipes become fades, no shake, no confetti.</summary>
    public static bool ReduceMotion
    {
        get => PlayerPrefs.GetInt("ui.reduceMotion", 0) == 1;
        set { PlayerPrefs.SetInt("ui.reduceMotion", value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
    }

    public Color Color(Tone tone) => tone switch
    {
        Tone.Ink => Ink,
        Tone.Studio => Studio,
        Tone.Riser => Riser,
        Tone.Haze => Haze,
        Tone.Paper => Paper,
        Tone.Brand => Brand,
        Tone.Accent => Accent,
        Tone.Good => Good,
        Tone.Warn => Warn,
        _ => Bad,
    };

    /// <summary>Text colour that reads on a fill of this tone.</summary>
    public Color TextOn(Tone fill) => fill is Tone.Accent or Tone.Paper or Tone.Good or Tone.Warn or Tone.Haze ? Ink : Paper;

    public float Size(TextRole role) => role switch
    {
        TextRole.DisplayXL => DisplayXL,
        TextRole.DisplayL => DisplayL,
        TextRole.DisplayM => DisplayM,
        TextRole.Title => Title,
        TextRole.Button => ButtonText,
        TextRole.Body => Body,
        _ => Label,
    };

    public TMP_FontAsset Font(TextRole role) => role switch
    {
        TextRole.DisplayXL or TextRole.DisplayL or TextRole.DisplayM => Display,
        TextRole.Title or TextRole.Button => UiBlack,
        _ => UiBold,
    };

    /// <summary>Spacing step <paramref name="n"/> of the scale 4, 8, 16, 24, 32, 48, 64.</summary>
    public float Gap(int n) => n switch { 0 => Space * 0.5f, 1 => Space, 2 => Space * 2f, 3 => Space * 3f, 4 => Space * 4f, 5 => Space * 6f, _ => Space * 8f };

    private void OnValidate() => Changed?.Invoke();
}
