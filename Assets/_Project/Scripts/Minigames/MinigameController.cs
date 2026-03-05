using Fusion;
using System;
using UnityEngine;
using Zenject;

public class MinigameController : NetworkBehaviour
{
    [Networked] public TickTimer CountdownTimer { get; private set; }
    [Networked] private int LastCountdownSecond { get; set; }
    [Networked] private bool IsCountingDown { get; set; }
    [Networked] public bool IsGameActive { get; protected set; }
    [Networked] private int ScoresReceived { get; set; }
    

    protected NetworkGameManager GameManager;
    protected LeaderboardController _leaderboard;

    [SerializeField] protected MinigameDefinitionSO _minigameDefinition;
    [SerializeField] protected bool _hasCountdown = true;

    [Inject] private BasePlayerSpawner _basePlayerSpawner;

    public event Action OnMinigameStart;
    public event Action OnMinigameEnd;
    public event Action OnCountdownStarted;
    public event Action<int> OnCountdownTick;

    private int _localScore;

    public override void Spawned()
    {
        GameManager = FindFirstObjectByType<NetworkGameManager>();
        _leaderboard = FindFirstObjectByType<LeaderboardController>();

        if (_leaderboard == null)
            Debug.LogWarning("LeaderboardController not found at Spawned — will retry in EndGame.");

        _localScore = 0;
        if (Object.HasInputAuthority)
            RPC_PlayerReady();
    }

    public void AddLocalScore(int points)
    {
        _localScore += points;
        Debug.Log($"[Local] Score this game: {_localScore}");
    }

    public void SetLocalScore(int score)
    {
        _localScore = score;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_PlayerReady()
    {
        GameManager.RegisterPlayerReady();
    }

    public void InitializeMinigame()
    {
        if (!HasStateAuthority)
            return;

        SetPlayersInput(false);

        if (_hasCountdown)
        {
            StartCountdown();
            return;
        }
        StartGame();
    }
    protected virtual void StartGame()
    {
        IsGameActive = true;

        RPC_OnGameStart();
        SetPlayersInput(true);

        Debug.Log($"Minigame {_minigameDefinition.Id} started!");
    }

    protected virtual void EndGame()
    {
        IsGameActive = false;
        //RPC_OnGameEnd();
        SetPlayersInput(false);
        Debug.Log($"Minigame {_minigameDefinition.Id} ended!");

        if (Object.HasInputAuthority)
            RPC_SubmitScore(Runner.LocalPlayer, _localScore);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SubmitScore(PlayerRef player, int score)
    {
        int slot = GameManager.GetSlotForPlayer(player);
        if (slot < 0)
        {
            Debug.LogWarning($"Unknown player {player} tried to submit score.");
            return;
        }

        ScoreBuffer.Record(Object.Id, slot, score);
        ScoresReceived++;

        Debug.Log($"[Host] Received score {score} from slot {slot} " +
                  $"({ScoresReceived}/{GameManager.ExpectedPlayerCount})");

        if (ScoresReceived >= GameManager.ExpectedPlayerCount)
            FinalizeScores();
    }
    private void FinalizeScores()
    {
        int[] scores = ScoreBuffer.FlushAndGet(Object.Id, GameManager.ExpectedPlayerCount);
        GameManager.SubmitMinigameScores(scores);

        _leaderboard.OnLeaderboardHidden += OnLeaderboardDone;
        _leaderboard.ShowLeaderboard(scores);

    }
    private void OnLeaderboardDone()
    {
        _leaderboard.OnLeaderboardHidden -= OnLeaderboardDone;
        RPC_OnGameEnd();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        UpdateGameStartCountdown();
    }

    protected void SetPlayersInput(bool value)
    {
        foreach (var playerEntry in _basePlayerSpawner.Players)
        {
            if (playerEntry.Value)
            {
                playerEntry.Value.Input.SetPlayerControlsActive(value);
            }
        }
    }

    private void StartCountdown()
    {
        CountdownTimer = TickTimer.CreateFromSeconds(Runner, _minigameDefinition.StartDelay);
        LastCountdownSecond = Mathf.CeilToInt(_minigameDefinition.StartDelay);
        IsCountingDown = true;
        RPC_CountdownStarted();
        RPC_UpdateCountdown((int)_minigameDefinition.StartDelay);
    }

    private void UpdateGameStartCountdown()
    {
        if (IsCountingDown)
        {
            float remaining = CountdownTimer.RemainingTime(Runner) ?? 0;

            if (remaining > 0)
            {
                int seconds = Mathf.CeilToInt(remaining);

                if (seconds != LastCountdownSecond)
                {
                    LastCountdownSecond = seconds;
                    RPC_UpdateCountdown(seconds);
                }
            }

            if (CountdownTimer.Expired(Runner))
            {
                IsCountingDown = false;
                StartGame();
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CountdownStarted()
    {
        OnCountdownStarted?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateCountdown(int seconds)
    {
        OnCountdownTick?.Invoke(seconds);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_OnGameStart()
    {
        OnMinigameStart?.Invoke();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    protected void RPC_OnGameEnd()
    {
        OnMinigameEnd?.Invoke();
    }
}
