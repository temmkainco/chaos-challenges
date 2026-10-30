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

    /// <summary>Where a wearable item hangs: on the head bone, the neck / upper chest, or the back of the chest.</summary>
    public enum Attach { Head, Neck, Back }

    /// <summary>The wearable slots (hat, face, neck, back), each its own list of items.</summary>
    public enum Slot { Hat, Face, Neck, Back }

    /// <summary>
    /// One wearable item. Older hats are placed in the head bone's own space (<see cref="LocalPosition"/>, scaled by
    /// the rig); items with <see cref="WorldSpace"/> are authored in metres in the character's frame (right, up,
    /// forward from the bone) with <see cref="Scale"/> as their size, so they work on any rig scale.
    /// </summary>
    [Serializable]
    public struct Hat
    {
        public string Name;
        public GameObject Prefab;
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public float Scale;
        public Attach AttachTo;
        public bool WorldSpace;
    }

    [field: SerializeField] public Swatch[] Colors { get; private set; } = Array.Empty<Swatch>();
    [field: SerializeField] public Hat[] Hats { get; private set; } = Array.Empty<Hat>();
    [field: SerializeField] public Hat[] Faces { get; private set; } = Array.Empty<Hat>();
    [field: SerializeField] public Hat[] Necks { get; private set; } = Array.Empty<Hat>();
    [field: SerializeField] public Hat[] Backs { get; private set; } = Array.Empty<Hat>();

    public Hat[] Items(Slot slot) => slot switch
    {
        Slot.Face => Faces,
        Slot.Neck => Necks,
        Slot.Back => Backs,
        _ => Hats,
    };

    public Hat GetItem(Slot slot, int index)
    {
        var items = Items(slot);
        return items.Length == 0 ? default : items[Mathf.Abs(index) % items.Length];
    }
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
