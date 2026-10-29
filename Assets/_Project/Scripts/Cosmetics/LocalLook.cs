using System;
using UnityEngine;

/// <summary>
/// The local player's character, chosen in the main menu customizer and remembered between sessions.
/// It is sent to the host every time the player spawns, so it follows you through every round.
/// </summary>
public static class LocalLook
{
    private const string HAT_KEY = "look.hat";

    public static event Action Changed;

    private static CosmeticsCatalog Catalog => CosmeticsCatalog.Instance;

    public static int GetColor(CosmeticsCatalog.Part part)
    {
        string key = Key(part);
        if (!PlayerPrefs.HasKey(key))
            return Catalog != null ? Catalog.DefaultFor(part) : 0;
        return PlayerPrefs.GetInt(key);
    }

    public static int Body => GetColor(CosmeticsCatalog.Part.Body);
    public static int Hands => GetColor(CosmeticsCatalog.Part.Hands);
    public static int Head => GetColor(CosmeticsCatalog.Part.Head);
    public static int Hat => PlayerPrefs.GetInt(HAT_KEY, 0);

    /// <summary>Steps a part's colour forwards or backwards through the catalogue.</summary>
    public static void StepColor(CosmeticsCatalog.Part part, int delta) =>
        Set(Key(part), GetColor(part) + delta, Catalog != null ? Catalog.Colors.Length : 1);

    public static void StepHat(int delta) => Set(HAT_KEY, Hat + delta, Catalog != null ? Catalog.Hats.Length : 1);

    /// <summary>A random look (the dice button).</summary>
    public static void Randomize()
    {
        int colors = Catalog != null ? Catalog.Colors.Length : 1;
        foreach (CosmeticsCatalog.Part part in Enum.GetValues(typeof(CosmeticsCatalog.Part)))
            PlayerPrefs.SetInt(Key(part), UnityEngine.Random.Range(0, colors));
        PlayerPrefs.SetInt(HAT_KEY, UnityEngine.Random.Range(0, Catalog != null ? Catalog.Hats.Length : 1));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static string Key(CosmeticsCatalog.Part part) => "look." + part.ToString().ToLowerInvariant();

    private static void Set(string key, int value, int count)
    {
        count = Mathf.Max(1, count);
        PlayerPrefs.SetInt(key, ((value % count) + count) % count);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
