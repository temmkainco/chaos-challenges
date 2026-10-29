using Fusion;
using Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public enum MatchPhase : byte
{
    Lobby,
    Intermission,
    Playing,
    RoundOver,
    FinalReveal,
}

public struct RosterEntry : INetworkStruct
{
    public PlayerRef Player;
    public NetworkString<_32> Name;
    public int Score;
    public int LastRoundPoints;
    public NetworkBool Connected;
}

/// <summary>
/// Host-authoritative owner of a match: roster, round order, scores, phase and scene flow.
/// Spawned once per session by <see cref="LobbyManager"/> and kept across scene loads,
/// so a rematch reuses it instead of creating a second one.
/// </summary>
public class NetworkGameManager : NetworkBehaviour, IPlayerLeft
{
    public const int MAX_ROUNDS = 16;

    public static NetworkGameManager Instance { get; private set; }

    [Networked, OnChangedRender(nameof(OnPhaseChanged))] public MatchPhase Phase { get; private set; }
    [Networked] public int RoundIndex { get; private set; }
    [Networked] public int RoundCount { get; private set; }
    [Networked] public int RosterCount { get; private set; }
    [Networked] public TickTimer PhaseTimer { get; private set; }

    [Networked, Capacity(MAX_ROUNDS)] private NetworkArray<int> RoundMinigames { get; }
    [Networked, Capacity(SessionSettings.MAX_PLAYERS)] private NetworkArray<RosterEntry> Roster { get; }

    // Scene load handshake: every peer reports once the scene for the current load id is active.
    [Networked] private int SceneLoadId { get; set; }
    [Networked] private int PendingSceneIndex { get; set; }

    [Header("Match")]
    [SerializeField, Tooltip("0 = every minigame once")] private int _roundsPerMatch;
    [SerializeField] private int _pointsPerPlace = 100;

    [Header("Timing")]
    [SerializeField] private float _roundOverDuration = 5f;
    [SerializeField] private float _finalRevealBaseDuration = 6f;
    [SerializeField] private float _finalRevealPerPlayer = 1.5f;
    [SerializeField] private float _sceneLoadTimeout = 20f;

    [Inject] private MinigameSceneDatabaseSO _database;

    /// <summary>Raised on every peer when <see cref="Phase"/> changes.</summary>
    public event Action<MatchPhase> PhaseChanged;

    private readonly HashSet<PlayerRef> _loadedPlayers = new();
    private int _reportedLoadId;
    private bool _waitingForLoad;
    private TickTimer _loadTimeout;

    public int ConnectedPlayerCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < RosterCount; i++)
                if (Roster[i].Connected) count++;
            return count;
        }
    }

    public IEnumerable<RosterEntry> Players
    {
        get
        {
            for (int i = 0; i < RosterCount; i++)
                yield return Roster[i];
        }
    }

    public bool IsLastRound => RoundIndex >= RoundCount - 1;
    public int CurrentMinigameIndex => RoundCount > 0 ? RoundMinigames[Mathf.Clamp(RoundIndex, 0, RoundCount - 1)] : -1;
    public MinigameDefinitionSO CurrentMinigame => GetDefinitionByIndex(CurrentMinigameIndex);
    public float PhaseTimeRemaining => PhaseTimer.RemainingTime(Runner) ?? 0f;

    public MinigameDefinitionSO GetDefinitionByIndex(int index) => _database?.GetByIndex(index);

    public bool IsConnectedPlayer(PlayerRef player)
    {
        int slot = GetSlot(player);
        return slot >= 0 && Roster[slot].Connected;
    }

    public override void Spawned()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_database == null)
            FindFirstObjectByType<SceneContext>()?.Container.Inject(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    #region Host API

    public void StartMatch()
    {
        if (!HasStateAuthority || Phase != MatchPhase.Lobby)
            return;

        if (_database == null || _database.Count == 0)
        {
            Debug.LogError("[Match] No minigames in the database; cannot start a match.");
            return;
        }

        BuildRoster();
        BuildRounds();
        SetSessionOpen(false);

        Debug.Log($"[Match] Starting: {RosterCount} players, {RoundCount} rounds");
        GoToIntermission();
    }

    public void ProceedFromIntermission()
    {
        if (!HasStateAuthority || Phase != MatchPhase.Intermission)
            return;

        var definition = CurrentMinigame;
        SetPhase(MatchPhase.Playing);
        LoadScene(definition.SceneBuildIndex);
    }

    /// <summary>
    /// Ends the current round. <paramref name="placements"/> is ordered best first;
    /// points go by place, and disconnected players get nothing.
    /// </summary>
    public void SubmitRoundResult(IReadOnlyList<PlayerRef> placements)
    {
        if (!HasStateAuthority || Phase != MatchPhase.Playing)
            return;

        for (int i = 0; i < RosterCount; i++)
        {
            var entry = Roster[i];
            entry.LastRoundPoints = 0;
            Roster.Set(i, entry);
        }

        int placeCount = placements.Count;
        for (int place = 0; place < placeCount; place++)
        {
            int slot = GetSlot(placements[place]);
            if (slot < 0 || !Roster[slot].Connected)
                continue;

            var entry = Roster[slot];
            entry.LastRoundPoints = (placeCount - place) * _pointsPerPlace;
            entry.Score += entry.LastRoundPoints;
            Roster.Set(slot, entry);

            Debug.Log($"[Match] Round {RoundIndex + 1}: #{place + 1} {entry.Name} +{entry.LastRoundPoints} (total {entry.Score})");
        }

        if (IsLastRound)
            SetPhase(MatchPhase.FinalReveal, _finalRevealBaseDuration + _finalRevealPerPlayer * RosterCount);
        else
            SetPhase(MatchPhase.RoundOver, _roundOverDuration);
    }

    #endregion

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        if (_waitingForLoad)
        {
            bool timedOut = _loadTimeout.Expired(Runner);
            if (AllPlayersLoaded() || timedOut)
            {
                if (timedOut)
                    Debug.LogWarning("[Match] Scene load timed out; continuing without the slow players.");

                _waitingForLoad = false;
                OnSceneReady();
            }
            return;
        }

        if (!PhaseTimer.Expired(Runner))
            return;

        PhaseTimer = TickTimer.None;
        switch (Phase)
        {
            case MatchPhase.RoundOver:
                RoundIndex++;
                GoToIntermission();
                break;
            case MatchPhase.FinalReveal:
                ReturnToLobby();
                break;
        }
    }

    public override void Render()
    {
        if (SceneLoadId == 0 || _reportedLoadId == SceneLoadId)
            return;

        if (Runner.IsSceneManagerBusy || SceneManager.GetActiveScene().buildIndex != PendingSceneIndex)
            return;

        _reportedLoadId = SceneLoadId;
        if (HasStateAuthority)
            MarkLoaded(Runner.LocalPlayer, SceneLoadId);
        else
            RPC_ReportSceneLoaded(SceneLoadId);
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        int slot = GetSlot(player);
        if (slot < 0)
            return;

        var entry = Roster[slot];
        entry.Connected = false;
        Roster.Set(slot, entry);
        Debug.Log($"[Match] {entry.Name} left");

        if (Phase == MatchPhase.Lobby)
            return;

        // A match that started with others cannot go on alone.
        if (RosterCount >= 2 && ConnectedPlayerCount < 2)
        {
            Debug.Log("[Match] Not enough players left; returning to lobby.");
            ReturnToLobby();
            return;
        }

        if (Phase == MatchPhase.Playing)
            FindFirstObjectByType<MinigameController>()?.HandlePlayerLeft(player);
        else if (Phase == MatchPhase.Intermission)
            FindFirstObjectByType<MinigameIntermissionController>()?.HandlePlayerLeft(player);
    }

    private void BuildRoster()
    {
        RosterCount = 0;
        foreach (var player in Runner.ActivePlayers.OrderBy(p => p.PlayerId))
        {
            if (RosterCount >= Roster.Length)
                break;

            string name = $"Player {player.PlayerId}";
            var playerObject = Runner.GetPlayerObject(player);
            if (playerObject != null && playerObject.TryGetComponent<Player>(out var playerComponent)
                && !string.IsNullOrEmpty(playerComponent.Nickname))
            {
                name = playerComponent.Nickname;
            }

            Roster.Set(RosterCount++, new RosterEntry { Player = player, Name = name, Connected = true });
        }

        for (int i = RosterCount; i < Roster.Length; i++)
            Roster.Set(i, default);
    }

    /// <summary>Random order without repeats; when there are more rounds than minigames, reshuffle without back-to-back repeats.</summary>
    private void BuildRounds()
    {
        int available = _database.Count;
        RoundCount = Mathf.Clamp(_roundsPerMatch > 0 ? _roundsPerMatch : available, 1, MAX_ROUNDS);

        var order = new List<int>(RoundCount + available);
        while (order.Count < RoundCount)
        {
            var bag = Enumerable.Range(0, available).OrderBy(_ => UnityEngine.Random.value).ToList();
            if (order.Count > 0 && bag.Count > 1 && bag[0] == order[^1])
                (bag[0], bag[1]) = (bag[1], bag[0]);
            order.AddRange(bag);
        }

        for (int i = 0; i < RoundCount; i++)
            RoundMinigames.Set(i, order[i]);

        RoundIndex = 0;
    }

    private void GoToIntermission()
    {
        SetPhase(MatchPhase.Intermission);
        LoadScene(GameScenes.IntermissionBuildIndex);
    }

    private void ReturnToLobby()
    {
        SetPhase(MatchPhase.Lobby);
        LoadScene(GameScenes.LobbyBuildIndex);
    }

    private void SetPhase(MatchPhase phase, float duration = 0f)
    {
        Phase = phase;
        PhaseTimer = duration > 0f ? TickTimer.CreateFromSeconds(Runner, duration) : TickTimer.None;
        Debug.Log($"[Match] Phase -> {phase}");
    }

    private void LoadScene(int buildIndex)
    {
        SceneLoadId++;
        PendingSceneIndex = buildIndex;
        _loadedPlayers.Clear();
        _waitingForLoad = true;
        _loadTimeout = TickTimer.CreateFromSeconds(Runner, _sceneLoadTimeout);

        Runner.LoadScene(SceneRef.FromIndex(buildIndex));
    }

    private void OnSceneReady()
    {
        switch (Phase)
        {
            case MatchPhase.Playing:
                var controller = FindFirstObjectByType<MinigameController>();
                if (controller == null)
                {
                    Debug.LogError($"[Match] No MinigameController in scene {PendingSceneIndex}; skipping round.");
                    SubmitRoundResult(Array.Empty<PlayerRef>());
                    return;
                }
                controller.InitializeMinigame();
                break;

            case MatchPhase.Lobby:
                SetSessionOpen(true);
                break;
        }
    }

    private bool AllPlayersLoaded()
    {
        for (int i = 0; i < RosterCount; i++)
        {
            var entry = Roster[i];
            if (entry.Connected && !_loadedPlayers.Contains(entry.Player))
                return false;
        }
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportSceneLoaded(int loadId, RpcInfo info = default)
    {
        MarkLoaded(info.Source, loadId);
    }

    private void MarkLoaded(PlayerRef player, int loadId)
    {
        if (loadId == SceneLoadId)
            _loadedPlayers.Add(player);
    }

    private void SetSessionOpen(bool open)
    {
        if (Runner.SessionInfo.IsValid)
            Runner.SessionInfo.IsOpen = open;
    }

    private int GetSlot(PlayerRef player)
    {
        for (int i = 0; i < RosterCount; i++)
            if (Roster[i].Player == player) return i;
        return -1;
    }

    private void OnPhaseChanged()
    {
        PhaseChanged?.Invoke(Phase);
    }
}
