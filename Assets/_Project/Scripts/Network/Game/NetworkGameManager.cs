using Fusion;
using UnityEngine;
using Zenject;

public class NetworkGameManager : NetworkBehaviour
{
    [Networked] public int CurrentMinigameIndex { get; private set; }
    private MinigameSceneDatabaseSO _database;

    public void Initialize(MinigameSceneDatabaseSO database)
    {
        _database = database;
    }

    public override void Spawned()
    {
        DontDestroyOnLoad(Object);
        if (HasStateAuthority)
        {
            CurrentMinigameIndex = 0;
        }
    }

    public void StartMatch()
    {
        if (!HasStateAuthority)
            return;

        Debug.Log("Starting Match");
        LoadMinigame(CurrentMinigameIndex);
    }

    public void FinishMinigame()
    {
        if (!HasStateAuthority)
            return;

        CurrentMinigameIndex++;
        LoadMinigame(CurrentMinigameIndex);
    }

    private void LoadMinigame(int index)
    {
        if (_database == null)
            return;

        var def = _database.GetByIndex(CurrentMinigameIndex);

        if (def == null)
            return;

        Runner.LoadScene(SceneRef.FromIndex(def.SceneBuildIndex));
    }
}
