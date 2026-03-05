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

    [Networked, Capacity(6)]
    public NetworkArray<NetworkString<_32>> PlayerNames { get; }

    private PlayerRef[] _playerSlots = new PlayerRef[6];

    [Inject] private MinigameSceneDatabaseSO _database;
    private const int LOBBY_SCENE_BUILD_INDEX = 2;
    private const int INTERMISSION_SCENE_BUILD_INDEX = 4;

    public MinigameDefinitionSO GetDefinitionByIndex(int index)
    {
        return _database?.GetByIndex(index);
    }

    public override void Spawned()
    {
        DontDestroyOnLoad(Object);

        var context = FindFirstObjectByType<SceneContext>();
        context.Container.Inject(this);

        if (HasStateAuthority)
        {
            CurrentMinigameIndex = 0;
        }
    }
    private void RegisterPlayerNames()
    {
        int slot = 0;
        foreach (var player in Runner.ActivePlayers)
        {
            string name = $"Player {player.PlayerId}"; 

            var networkObject = Runner.GetPlayerObject(player);
            if (networkObject != null)
            {
                var playerComponent = networkObject.GetComponent<Player>();
                if (playerComponent != null && !string.IsNullOrEmpty(playerComponent.Nickname))
                    name = playerComponent.Nickname;
            }

            PlayerNames.Set(slot, name);
            slot++;
        }
    }
    public bool IsLastMinigame()
    {
        return CurrentMinigameIndex >= _database.Count - 1;
    }
    public string GetPlayerName(int slot)
    {
        if (slot < 0 || slot >= ExpectedPlayers) return "";
        string name = PlayerNames[slot].ToString();
        return string.IsNullOrEmpty(name) ? $"Player {slot + 1}" : name;
    }

    public int GetTotalScore(int slot)
    {
        if (slot < 0 || slot >= ExpectedPlayers) return 0;
        return GlobalScores[slot];
    }
    public MinigameDefinitionSO GetCurrentMinigameDefinition()
    {
        return _database.GetByIndex(CurrentMinigameIndex);
    }
    public void ProceedFromIntermission()
    {
        if (!HasStateAuthority) return;
        PlayersReady = 0;
        LoadMinigameScene(CurrentMinigameIndex); // ← was LoadMinigame, infinite loop
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
        RegisterPlayerNames();
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
            StartCoroutine(DelayedFinishMatch());
            return;
        }

        PlayersReady = 0;
        LoadMinigame(CurrentMinigameIndex);
    }

    private IEnumerator DelayedFinishMatch()
    {
        yield return new WaitForSeconds(1f);
        FinishMatch();
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

        // 2. Load intermission scene (curtain is closed so load is invisible)
        await Runner.LoadScene(SceneRef.FromIndex(INTERMISSION_SCENE_BUILD_INDEX));

        if (HasStateAuthority)
            StartCoroutine(WaitForIntermission());
    }

    private IEnumerator WaitForIntermission()
    {
        MinigameIntermissionController controller = null;
        while (controller == null)
        {
            controller = FindFirstObjectByType<MinigameIntermissionController>();
            yield return null;
        }
        // Intermission drives itself from here — no further action needed
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
    private async void LoadMinigameScene(int index)
    {
        var def = _database.GetByIndex(index);
        if (def == null) return;

        await Runner.LoadScene(SceneRef.FromIndex(def.SceneBuildIndex));

        if (HasStateAuthority)
            StartCoroutine(WaitForMinigameAndStart());
    }
}
