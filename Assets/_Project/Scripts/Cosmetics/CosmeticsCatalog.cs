using System;
using UnityEngine;

/// <summary>Everything a contestant can wear. Loaded from Resources so network-spawned players can find it.</summary>
[CreateAssetMenu(menuName = "Game/Cosmetics Catalog")]
public class CosmeticsCatalog : ScriptableObject
{
    public const string RESOURCE_PATH = "Cosmetics";

    /// <summary>Body parts that can be coloured; the values are the submesh indices of the character mesh.</summary>
    public enum Part { Body = 0, Hands = 1, Head = 2 }

    [Serializable]
    public struct Swatch
    {
        public string Name;
        public Color Color;
    }

    [Serializable]
    public struct Hat
    {
        public string Name;
        public GameObject Prefab;
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public float Scale;
    }

    [field: SerializeField] public Swatch[] Colors { get; private set; } = Array.Empty<Swatch>();
    [field: SerializeField] public Hat[] Hats { get; private set; } = Array.Empty<Hat>();
    [field: SerializeField] public int DefaultBody { get; private set; }
    [field: SerializeField] public int DefaultHands { get; private set; }
    [field: SerializeField] public int DefaultHead { get; private set; }

    private static CosmeticsCatalog _instance;
    public static CosmeticsCatalog Instance => _instance != null ? _instance : _instance = Resources.Load<CosmeticsCatalog>(RESOURCE_PATH);

    public Swatch GetColor(int index) => Colors.Length == 0 ? new Swatch { Name = "-", Color = Color.white } : Colors[Mathf.Abs(index) % Colors.Length];
    public Hat GetHat(int index) => Hats.Length == 0 ? default : Hats[Mathf.Abs(index) % Hats.Length];

    public int DefaultFor(Part part) => part switch
    {
        Part.Hands => DefaultHands,
        Part.Head => DefaultHead,
        _ => DefaultBody,
    };
}
