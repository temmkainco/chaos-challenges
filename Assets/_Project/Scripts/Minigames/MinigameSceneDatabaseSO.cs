using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Minigame Scene Database")]
public class MinigameSceneDatabaseSO : ScriptableObject
{
    [field: SerializeField] public List<MinigameDefinitionSO> Minigames { get; private set; } = new();

    public int Count => Minigames.Count;

    public MinigameDefinitionSO GetByIndex(int index)
    {
        if (index < 0 || index >= Minigames.Count)
            return null;
        
        return Minigames[index];
    }

    public int GetSceneIndex(int index)
    {
        var def = GetByIndex(index);
        return def != null ? def.SceneBuildIndex : -1;
    }

}
