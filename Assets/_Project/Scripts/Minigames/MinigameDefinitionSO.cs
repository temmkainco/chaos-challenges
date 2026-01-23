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

    [Header("Presentation")]
    [field: SerializeField] public string DisplayName { get; private set; }
}
