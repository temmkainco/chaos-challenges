using Fusion;
using System.Collections;
using System.Linq;
using UnityEngine;
using Zenject;

public class NetworkGameManager : NetworkBehaviour
{
    [Networked] public int CurrentMinigameIndex { get; private set; }
    [Networked] private int PlayersReady { get; set; }
    [Networked] private int ExpectedPlayers { get; set; }

    private MinigameSceneDatabaseSO _database;
    private const int LOBBY_SCENE_BUILD_INDEX = 2;

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

    public void RegisterPlayerReady()
    {
        if (!HasStateAuthority)
            return;

        PlayersReady++;

        if (PlayersReady >= ExpectedPlayers)
        {
            StartCoroutine(WaitForMinigameAndStart());
        }
    }

    public void StartMatch()
    {
        if (!HasStateAuthority)
            return;

        Debug.Log("Starting Match");

        ExpectedPlayers = Runner.ActivePlayers.Count();
        PlayersReady = 0;

        LoadMinigame(CurrentMinigameIndex);
    }

    public void FinishMinigame()
    {
        if (!HasStateAuthority)
            return;

        Debug.Log("NetworkGameManager: Minigame Finished");
        
        CurrentMinigameIndex++;
        if (CurrentMinigameIndex >= _database.Count)
        {
            FinishMatch();
            return;
        }
        LoadMinigame(CurrentMinigameIndex);
    }

    private async void FinishMatch()
    {
        if (!HasStateAuthority)
            return;

        Debug.Log("Match Finished! Returning to lobby.");

        await System.Threading.Tasks.Task.Delay(3000);
        SceneRef lobbyScene = SceneRef.FromIndex(LOBBY_SCENE_BUILD_INDEX);
        await Runner.LoadScene(lobbyScene);
    }

    private async void LoadMinigame(int index)
    {
        var def = _database.GetByIndex(index);
        if (def == null)
            return;

        await Runner.LoadScene(SceneRef.FromIndex(def.SceneBuildIndex));

        if (HasStateAuthority)
        {
            StartCoroutine(WaitForMinigameAndStart());
        }
    }

    private IEnumerator WaitForMinigameAndStart()
    {
        MinigameController controller = null;

        while (controller == null)
        {
            controller = FindFirstObjectByType<MinigameController>();
            yield return null;
        }

        controller.InitializeMinigame();
        controller.OnMinigameEnd += FinishMinigame;
    }
}
