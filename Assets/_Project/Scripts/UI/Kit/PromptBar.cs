using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Button prompts, always in the same corner (bottom right of the safe area): a key cap and a short verb per action.
/// The caps follow the device last used: keyboard keys, or gamepad buttons once a pad is touched.
/// </summary>
public class PromptBar : MonoBehaviour
{
    /// <summary>The actions prompts can name, with their keyboard and gamepad caps.</summary>
    public enum Action { Confirm, Back, Interact, Jump, Emote, Players, Games, Throw, Colour }

    private static readonly Dictionary<Action, (string key, string pad)> Caps = new()
    {
        [Action.Confirm] = ("Enter", "A"),
        [Action.Back] = ("Esc", "B"),
        [Action.Interact] = ("E", "Y"),
        [Action.Jump] = ("Space", "A"),
        [Action.Emote] = ("T", "D-pad Up"),
        [Action.Players] = ("Tab", "View"),
        [Action.Games] = ("L", "LB"),
        [Action.Throw] = ("LMB", "RT"),
        [Action.Colour] = ("C", "RB"),
    };

    /// <summary>True once a gamepad was used more recently than the keyboard or mouse.</summary>
    public static bool UsingGamepad { get; private set; }

    private readonly List<(Action action, TextMeshProUGUI cap)> _items = new();
    private bool _shownPad;

    public static PromptBar Create(Transform parent, params (Action action, string verb)[] prompts)
    {
        var box = Ui.Box(parent, "Prompts", UiTheme.Tone.Studio);
        var rt = (RectTransform)box.transform;
        Ui.Place(rt, new Vector2(1f, 0f), Vector2.zero, new Vector2(10f, 76f));
        var bar = rt.gameObject.AddComponent<PromptBar>();
        var row = Ui.Rect(rt, "Row");
        Ui.Stretch(row);
        var layout = Ui.Stack<HorizontalLayoutGroup>(row, 4, TextAnchor.MiddleCenter);
        layout.padding = new RectOffset(18, 24, 0, 0);
        var fit = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        fit.childControlWidth = fit.childControlHeight = true;
        foreach (Transform layer in rt) if (layer != row) layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        rt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var (action, verb) in prompts)
        {
            var item = Ui.Rect(row, verb);
            Ui.Stack<HorizontalLayoutGroup>(item, 1, TextAnchor.MiddleLeft);
            var cap = Ui.Box(item, "Cap", UiTheme.Tone.Paper, 10f);
            cap.ShadowDepth = UiTheme.Instance.ShadowChip;
            cap.Apply();
            var capText = Ui.Text(cap.transform, "", UiTheme.TextRole.Label, UiTheme.Tone.Ink);
            capText.GetComponent<ThemedText>().ShrinkToFit = false;
            Ui.Stretch(capText.rectTransform, 6f);
            Ui.Size(cap, 56f, 48f);
            var label = Ui.Text(item, verb, UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
            label.fontStyle = FontStyles.Bold;
            bar._items.Add((action, capText));
        }
        bar.Refresh(true);
        return bar;
    }

    private static readonly string[] MoveKeys = { "WASD", "W A S D", "A / D", "W / S", "W", "S", "A", "D", "ARROWS" };

    /// <summary>
    /// A keyboard control as written in game data ("SPACE", "Hold LMB", "E / LMB") in the device last used: on a
    /// gamepad movement keys become the left stick and the rest map word by word to the player bindings.
    /// </summary>
    public static string Cap(string key)
    {
        if (!UsingGamepad || string.IsNullOrEmpty(key)) return key;
        if (System.Array.IndexOf(MoveKeys, key.Trim().ToUpperInvariant()) >= 0) return "L-Stick";
        return System.Text.RegularExpressions.Regex.Replace(key, @"\b(LMB|SPACE|SHIFT|E)\b", m => m.Value switch
        {
            "LMB" => "X",
            "SPACE" => "A",
            "SHIFT" => "L3",
            _ => "Y",
        });
    }

    private void Update()
    {
        var pad = Gamepad.current;
        if (pad != null && pad.wasUpdatedThisFrame && IsActuated(pad)) UsingGamepad = true;
        else if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 4f))
            UsingGamepad = false;
        Refresh(false);
    }

    private static bool IsActuated(Gamepad pad)
    {
        foreach (var control in pad.allControls)
            if (control is UnityEngine.InputSystem.Controls.ButtonControl b && b.isPressed) return true;
        return pad.leftStick.ReadValue().sqrMagnitude > 0.2f || pad.rightStick.ReadValue().sqrMagnitude > 0.2f;
    }

    private void Refresh(bool force)
    {
        if (!force && _shownPad == UsingGamepad) return;
        _shownPad = UsingGamepad;
        foreach (var (action, cap) in _items)
        {
            var (key, padCap) = Caps[action];
            cap.text = UsingGamepad ? padCap : key;
            Ui.Size(cap.transform.parent.GetComponent<UiBox>(), Mathf.Max(56f, cap.text.Length * 20f + 28f), 48f);
        }
    }
}
