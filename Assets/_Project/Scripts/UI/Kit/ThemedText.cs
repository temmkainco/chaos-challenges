using TMPro;
using UnityEngine;

/// <summary>
/// Text in one of the theme's roles (font, size, colour). Display roles over the 3D world use the outlined material.
/// Long strings (translations) may shrink to 80% of the role size, never below the theme's minimum.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
[DisallowMultipleComponent]
public class ThemedText : MonoBehaviour
{
    public UiTheme.TextRole Role = UiTheme.TextRole.Body;
    public UiTheme.Tone Tone = UiTheme.Tone.Paper;
    [Tooltip("Thick Ink outline (display text straight over the 3D world)")] public bool Outlined;
    [Tooltip("Shrink to fit (up to 80%) instead of overflowing; leave room for translations")] public bool ShrinkToFit = true;
    [Tooltip("Never wrap (names, tags); Body and Label text wraps otherwise")] public bool SingleLine;
    [Tooltip("Letter spacing; negative = the role's")] public float Spacing = -1f;

    private TextMeshProUGUI _text;

    public TextMeshProUGUI Text => _text != null ? _text : _text = GetComponent<TextMeshProUGUI>();

    private void OnEnable() { UiTheme.Changed += Apply; Apply(); }
    private void OnDisable() => UiTheme.Changed -= Apply;

    public void Set(UiTheme.TextRole role, UiTheme.Tone tone, bool outlined = false)
    {
        Role = role; Tone = tone; Outlined = outlined;
        Apply();
    }

    public void Apply()
    {
        var theme = UiTheme.Instance;
        var text = Text;
        var font = theme.Font(Role);
        if (font != null) text.font = font;
        bool display = Role is UiTheme.TextRole.DisplayXL or UiTheme.TextRole.DisplayL or UiTheme.TextRole.DisplayM;
        if (display && Outlined && theme.DisplayOutline != null) text.fontSharedMaterial = theme.DisplayOutline;
        else if (font != null) text.fontSharedMaterial = font.material;
        float size = theme.Size(Role);
        text.fontSize = size;
        text.enableAutoSizing = ShrinkToFit;
        text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Max(theme.Label, size * 0.8f);
        text.color = theme.Color(Tone);
        text.textWrappingMode = !SingleLine && Role is UiTheme.TextRole.Body or UiTheme.TextRole.Label ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.characterSpacing = Spacing >= 0f ? Spacing : Role is UiTheme.TextRole.Button or UiTheme.TextRole.Label ? 3f : 0f;
        text.raycastTarget = false;
    }
}
