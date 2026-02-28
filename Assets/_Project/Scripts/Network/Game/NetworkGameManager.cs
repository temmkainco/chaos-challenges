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

    [Networked, Capacity(6)]
    public NetworkArray<int> GlobalScores { get; }

    private PlayerRef[] _playerSlots = new PlayerRef[6];

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
        int slot = 0;
        foreach (var player in Runner.ActivePlayers)
            _playerSlots[slot++] = player;

        ExpectedPlayers = Runner.ActivePlayers.Count();
        PlayersReady = 0;

        LoadMinigame(CurrentMinigameIndex);
    }
    /// <summary>Called by clients for non-elimination minigames.</summary>
    public void SubmitMinigameScores(int[] scoresBySlot)
    {
        if (!HasStateAuthority) return;

        Debug.Log($"=== Minigame {CurrentMinigameIndex} Results ===");
        for (int i = 0; i < ExpectedPlayers; i++)
        {
            GlobalScores.Set(i, GlobalScores[i] + scoresBySlot[i]);
            Debug.Log($"  Player slot {i} | This game: {scoresBySlot[i]} | Total: {GlobalScores[i]}");
        }
    }

    /// <summary>Called directly by host-authoritative systems (e.g. elimination ranking).</summary>
    public void AddToGlobalScore(int slot, int score)
    {
        if (!HasStateAuthority) return;
        GlobalScores.Set(slot, GlobalScores[slot] + score);
    }

    public void FinishMinigame()
    {
        if (!HasStateAuthority) return;

        Debug.Log("NetworkGameManager: Minigame Finished");

        CurrentMinigameIndex++;
        if (CurrentMinigameIndex >= _database.Count)
        {
            FinishMatch();
            return;
        }

        PlayersReady = 0;
        LoadMinigame(CurrentMinigameIndex);
    }

    private async void FinishMatch()
    {
        if (!HasStateAuthority) return;

        Debug.Log("=== Match Over! Final Scores ===");

        var ranked = Enumerable.Range(0, ExpectedPlayers)
            .Select(i => (slot: i, score: GlobalScores[i]))
            .OrderByDescending(x => x.score)
            .ToList();

        for (int rank = 0; rank < ranked.Count; rank++)
            Debug.Log($"  #{rank + 1} Player slot {ranked[rank].slot} — {ranked[rank].score} pts");

        await System.Threading.Tasks.Task.Delay(3000);
        SceneRef lobbyScene = SceneRef.FromIndex(LOBBY_SCENE_BUILD_INDEX);
        await Runner.LoadScene(lobbyScene);
    }
    public int GetSlotForPlayer(PlayerRef player)
    {
        for (int i = 0; i < _playerSlots.Length; i++)
            if (_playerSlots[i] == player) return i;
        return -1;
    }
    public int ExpectedPlayerCount => ExpectedPlayers;

    private async void LoadMinigame(int index)
    {
        var def = _database.GetByIndex(index);
        if (def == null) return;

        await Runner.LoadScene(SceneRef.FromIndex(def.SceneBuildIndex));

        if (HasStateAuthority)
            StartCoroutine(WaitForMinigameAndStart());
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
