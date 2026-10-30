using UnityEngine;

public enum MinigameType
{
    Timer,
    Score,
    Elimination,
    Placement
}

[CreateAssetMenu(menuName = "Game/Minigame Definition")]
public class MinigameDefinitionSO : ScriptableObject
{
    [Header("Identification")]
    [field: SerializeField] public string Id { get; private set; }

    [Header("Scene")]
    [field: SerializeField] public int SceneBuildIndex { get; private set; }

    [Header("Rules")]
    [field: SerializeField] public MinigameType GameType { get; private set; }
    [field: SerializeField] public float GameDuration { get; private set; }
    [field: SerializeField] public bool IsTimeLimited {  get; private set; }
    [field: SerializeField] public float StartDelay { get; private set; }

    [Header("Presentation")]
    [field: SerializeField] public string DisplayName { get; private set; }
    [field: SerializeField] public string Description { get; private set; }
    [field: SerializeField] public Sprite PreviewImage { get; private set; }
    [field: SerializeField] public string[] Rules { get; private set; }

    [Header("Rules card")]
    [field: SerializeField, Tooltip("How you win, one short line")] public string Objective { get; private set; }
    [field: SerializeField, Tooltip("What knocks you out or costs you, one short line")] public string Danger { get; private set; }
    [field: SerializeField, Tooltip("How places and points are decided, one short line")] public string Scoring { get; private set; }
    [field: SerializeField, Tooltip("Key|Action pairs, e.g. \"E|Grab a bomb\"")] public string[] Controls { get; private set; }
}
