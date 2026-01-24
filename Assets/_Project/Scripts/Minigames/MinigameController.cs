using Fusion;
using System;
using UnityEngine;
using Zenject;

public class MinigameController : NetworkBehaviour
{
    [Networked] public TickTimer CountdownTimer { get; private set; }
    [Networked] private int LastCountdownSecond { get; set; }
    [Networked] private bool IsCountingDown { get; set; }
    [Networked] public TickTimer GameTimer { get; protected set; }
    [Networked] public bool IsGameActive { get; protected set; }

    protected NetworkGameManager GameManager;

    [SerializeField] protected MinigameDefinitionSO _minigameDefinition;
    [Inject] private BasePlayerSpawner _basePlayerSpawner;

    public event Action OnMinigameStart;
    public event Action OnMinigameEnd;
    public event Action OnCountdownStarted;
    public event Action<int> OnCountdownTick;

    public override void Spawned()
    {
        GameManager = FindFirstObjectByType<NetworkGameManager>();

        if (HasStateAuthority)
        {
            StartCountdown();
        }
    }

    protected void SetPlayersInput(bool value)
    {
        foreach (var playerEntry in _basePlayerSpawner.Players)
        {
            playerEntry.Value.Input.SetPlayerControlsActive(value);
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

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

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


    protected virtual void StartGame()
    {
        IsGameActive = true;
        GameTimer = TickTimer.CreateFromSeconds(Runner, _minigameDefinition.GameDuration);

        RPC_OnGameStart();
        SetPlayersInput(true);
        OnMinigameStart?.Invoke();

        Debug.Log($"Minigame {_minigameDefinition.Id} started! Duration: {_minigameDefinition.GameDuration}s");
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
