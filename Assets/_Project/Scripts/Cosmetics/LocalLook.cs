using System;
using UnityEngine;

/// <summary>
/// The local player's character, chosen in the main menu customizer and remembered between sessions.
/// It is sent to the host every time the player spawns, so it follows you through every round.
/// Part colours are free RGB colours (saved as hex, "look.body.rgb"); looks saved before the colour picker kept a
/// catalogue index ("look.body"), which is read once as that swatch's colour.
/// </summary>
public static class LocalLook
{
    private const string HAT_KEY = "look.hat";

    public static event Action Changed;

    private static CosmeticsCatalog Catalog => CosmeticsCatalog.Instance;

    public static Color GetColor(CosmeticsCatalog.Part part)
    {
        if (TryParseHex(PlayerPrefs.GetString(RgbKey(part), ""), out var color))
            return color;
        string key = Key(part);
        int index = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : Catalog != null ? Catalog.DefaultFor(part) : 0;
        return Catalog != null ? Catalog.GetColor(index).Color : Color.white;
    }

    public static Color Body => GetColor(CosmeticsCatalog.Part.Body);
    public static Color Hands => GetColor(CosmeticsCatalog.Part.Hands);
    public static Color Head => GetColor(CosmeticsCatalog.Part.Head);
    public static int Hat => PlayerPrefs.GetInt(HAT_KEY, 0);
    public static int Face => PlayerPrefs.GetInt("look.face", 0);
    public static int Neck => PlayerPrefs.GetInt("look.neck", 0);
    public static int Back => PlayerPrefs.GetInt("look.back", 0);

    public static int GetItem(CosmeticsCatalog.Slot slot) => slot switch
    {
        CosmeticsCatalog.Slot.Face => Face,
        CosmeticsCatalog.Slot.Neck => Neck,
        CosmeticsCatalog.Slot.Back => Back,
        _ => Hat,
    };

    private static string ItemKey(CosmeticsCatalog.Slot slot) => slot == CosmeticsCatalog.Slot.Hat ? HAT_KEY : "look." + slot.ToString().ToLowerInvariant();

    /// <summary>Steps a wearable slot forwards or backwards through its items (index 0 is "none").</summary>
    public static void StepItem(CosmeticsCatalog.Slot slot, int delta) =>
        Set(ItemKey(slot), GetItem(slot) + delta, Catalog != null ? Catalog.Items(slot).Length : 1);

    /// <summary>Steps a part's colour forwards or backwards through the catalogue's presets (from the nearest one).</summary>
    public static void StepColor(CosmeticsCatalog.Part part, int delta)
    {
        if (Catalog == null || Catalog.Colors.Length == 0) return;
        int count = Catalog.Colors.Length;
        int index = (NearestPreset(GetColor(part)) + delta + count) % count;
        SetColor(part, Catalog.Colors[index].Color);
    }

    /// <summary>
    /// Sets a part's colour. <paramref name="save"/> false while a picker is being dragged (the preview and the
    /// network follow; the disk is written when the drag ends).
    /// </summary>
    public static void SetColor(CosmeticsCatalog.Part part, Color color, bool save = true)
    {
        PlayerPrefs.SetString(RgbKey(part), ToHex(color));
        if (save) PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>The preset with this colour (the closest one), for names and stepping.</summary>
    public static int NearestPreset(Color color)
    {
        if (Catalog == null) return 0;
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < Catalog.Colors.Length; i++)
        {
            var c = Catalog.Colors[i].Color;
            float d = (c.r - color.r) * (c.r - color.r) + (c.g - color.g) * (c.g - color.g) + (c.b - color.b) * (c.b - color.b);
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    /// <summary>The preset's name if the colour is one, else its hex code.</summary>
    public static string NameOf(Color color)
    {
        if (Catalog != null && Catalog.Colors.Length > 0)
        {
            var preset = Catalog.Colors[NearestPreset(color)];
            if (ToHex(preset.Color) == ToHex(color)) return preset.Name;
        }
        return "#" + ToHex(color);
    }

    public static string ToHex(Color color) => ColorUtility.ToHtmlStringRGB(color);

    /// <summary>"RRGGBB" or "#RRGGBB" (3-digit "#RGB" too).</summary>
    public static bool TryParseHex(string hex, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (hex.Length != 4 && hex.Length != 7) return false;
        return ColorUtility.TryParseHtmlString(hex, out color);
    }

    /// <summary>A colour as 24-bit RGB, for the network.</summary>
    public static int ToRgb(Color color)
    {
        Color32 c = color;
        return (c.r << 16) | (c.g << 8) | c.b;
    }

    public static Color FromRgb(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    public static void StepHat(int delta) => Set(HAT_KEY, Hat + delta, Catalog != null ? Catalog.Hats.Length : 1);

    /// <summary>A random look (the dice button).</summary>
    public static void Randomize()
    {
        int colors = Catalog != null ? Catalog.Colors.Length : 0;
        if (colors > 0)
            foreach (CosmeticsCatalog.Part part in Enum.GetValues(typeof(CosmeticsCatalog.Part)))
                PlayerPrefs.SetString(RgbKey(part), ToHex(Catalog.Colors[UnityEngine.Random.Range(0, colors)].Color));
        foreach (CosmeticsCatalog.Slot slot in Enum.GetValues(typeof(CosmeticsCatalog.Slot)))
        {
            int count = Catalog != null ? Catalog.Items(slot).Length : 1;
            // Hats always, the other slots about half the time, so random looks aren't overloaded.
            PlayerPrefs.SetInt(ItemKey(slot), slot == CosmeticsCatalog.Slot.Hat || UnityEngine.Random.value < 0.5f ? UnityEngine.Random.Range(0, count) : 0);
        }
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static string Key(CosmeticsCatalog.Part part) => "look." + part.ToString().ToLowerInvariant();
    private static string RgbKey(CosmeticsCatalog.Part part) => Key(part) + ".rgb";

    private static void Set(string key, int value, int count)
    {
        count = Mathf.Max(1, count);
        PlayerPrefs.SetInt(key, ((value % count) + count) % count);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
