using Fusion;
using System;
using UnityEngine;
using Zenject;

//This class exists on the minigame scene and is responsible for managing the minigame flow (countdown, start, end) and syncing it across the network.
//It also provides events for the minigame UI to react to changes in the game state.

public class MinigameController : NetworkBehaviour
{
    [Networked] public TickTimer CountdownTimer { get; private set; }
    [Networked] private int LastCountdownSecond { get; set; }
    [Networked] private bool IsCountingDown { get; set; }
    [Networked] public bool IsGameActive { get; protected set; }

    protected NetworkGameManager GameManager;

    [SerializeField] protected MinigameDefinitionSO _minigameDefinition;
    [SerializeField] protected bool _hasCountdown = true;

    [Inject] private BasePlayerSpawner _basePlayerSpawner;

    public event Action OnMinigameStart;
    public event Action OnMinigameEnd;
    public event Action OnCountdownStarted;
    public event Action<int> OnCountdownTick;

    public override void Spawned()
    {
        GameManager = FindFirstObjectByType<NetworkGameManager>();
        if (Object.HasInputAuthority)
        {
            RPC_PlayerReady();
        }
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

        RPC_OnGameEnd();
        SetPlayersInput(false);

        Debug.Log($"Minigame {_minigameDefinition.Id} ended!");
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
