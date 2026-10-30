using TMPro;
using UnityEngine;

/// <summary>The cartoon UI kit (rounded sprites, font, palette) for UI built from code. Lives in Resources/UiKit.</summary>
[CreateAssetMenu(menuName = "Game/UI Kit")]
public class UiKit : ScriptableObject
{
    public static readonly Color Ink = new(0.18f, 0.1f, 0.28f);
    public static readonly Color Cream = new(1f, 0.965f, 0.89f);
    public static readonly Color Yellow = new(1f, 0.79f, 0.24f);
    public static readonly Color Orange = new(1f, 0.54f, 0.24f);
    public static readonly Color DeepOrange = new(0.91f, 0.35f, 0.05f);
    public static readonly Color Pink = new(1f, 0.49f, 0.71f);
    public static readonly Color Lavender = new(0.91f, 0.87f, 0.98f);
    public static readonly Color Mint = new(0.31f, 0.82f, 0.63f);
    public static readonly Color Tomato = new(1f, 0.37f, 0.36f);

    public Sprite Panel;
    public Sprite Button;
    public Sprite Circle;
    public TMP_FontAsset Font;
    public Material OutlineThick;
    public Material OutlineThin;

    private static UiKit _instance;
    public static UiKit Instance => _instance != null ? _instance : _instance = Resources.Load<UiKit>("UiKit");
}
