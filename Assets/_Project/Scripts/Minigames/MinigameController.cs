using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public enum MinigamePhase : byte
{
    Waiting,
    Countdown,
    Playing,
    Finished,
}

/// <summary>
/// Base for every minigame. The host drives a networked <see cref="Phase"/> with one <see cref="PhaseTimer"/>
/// (start countdown, then the optional time limit); every peer raises the UI events from the replicated state.
/// Subclasses end the round with <see cref="FinishRound(IReadOnlyList{PlayerRef})"/> and can override
/// <see cref="HandlePlayerLeft"/> and <see cref="GetTimeUpStandings"/>.
/// </summary>
public class MinigameController : NetworkBehaviour
{
    /// <summary>Seconds before the time limit runs out from which the countdown UI shows the remaining time.</summary>
    public const int FINAL_SECONDS = 10;

    [Networked, OnChangedRender(nameof(OnPhaseChanged))] public MinigamePhase Phase { get; private set; }
    [Networked] public TickTimer PhaseTimer { get; private set; }

    public bool IsGameActive => Phase == MinigamePhase.Playing;
    public bool IsTimeLimited => _minigameDefinition.IsTimeLimited && _minigameDefinition.GameDuration > 0f;
    public float TimeRemaining => PhaseTimer.RemainingTime(Runner) ?? 0f;

    protected NetworkGameManager GameManager => NetworkGameManager.Instance;

    [SerializeField] protected MinigameDefinitionSO _minigameDefinition;
    [SerializeField] protected bool _hasCountdown = true;

    [Inject] protected BasePlayerSpawner _playerSpawner;

    public event Action OnMinigameStart;
    public event Action OnMinigameEnd;
    public event Action OnCountdownStarted;
    public event Action<int> OnCountdownTick;
    /// <summary>The last <see cref="FINAL_SECONDS"/> of a time-limited round, once per second.</summary>
    public event Action<int> OnTimeLimitTick;

    private int _lastShownSecond = -1;

    /// <summary>Called by <see cref="NetworkGameManager"/> on the host once every player has loaded the scene.</summary>
    public void InitializeMinigame()
    {
        if (!HasStateAuthority || Phase != MinigamePhase.Waiting)
            return;

        SetPlayersInput(false);

        if (_hasCountdown && _minigameDefinition.StartDelay > 0f)
        {
            SetPhase(MinigamePhase.Countdown, _minigameDefinition.StartDelay);
            return;
        }
        StartGame();
    }

    /// <summary>Host only. A player disconnected during this round.</summary>
    public virtual void HandlePlayerLeft(PlayerRef player) { }

    /// <summary>Host only. Opens the round: players can move. Overrides call the base first.</summary>
    protected virtual void StartGame()
    {
        SetPhase(MinigamePhase.Playing, IsTimeLimited ? _minigameDefinition.GameDuration : 0f);
        SetPlayersInput(true);

        Debug.Log($"Minigame {_minigameDefinition.Id} started!");
    }

    /// <summary>Host only. Ends the round; <paramref name="placements"/> is ordered best first, one player per place.</summary>
    protected void FinishRound(IReadOnlyList<PlayerRef> placements)
    {
        FinishRound(placements.Select(p => (IReadOnlyList<PlayerRef>)new[] { p }).ToList());
    }

    /// <summary>Host only. Ends the round; <paramref name="standings"/> is ordered best first and players in one group tie.</summary>
    protected void FinishRound(IReadOnlyList<IReadOnlyList<PlayerRef>> standings)
    {
        if (!HasStateAuthority || !IsGameActive)
            return;

        SetPhase(MinigamePhase.Finished);
        SetPlayersInput(false);
        Debug.Log($"Minigame {_minigameDefinition.Id} ended!");

        GameManager.SubmitRoundResult(standings);
    }

    /// <summary>Host only. Standings when the time limit runs out; by default every connected player ties.</summary>
    protected virtual IReadOnlyList<IReadOnlyList<PlayerRef>> GetTimeUpStandings()
    {
        return new[] { (IReadOnlyList<PlayerRef>)ConnectedPlayers().ToList() };
    }

    protected IEnumerable<PlayerRef> ConnectedPlayers() => Runner.ActivePlayers.Where(GameManager.IsConnectedPlayer);

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !PhaseTimer.Expired(Runner))
            return;

        switch (Phase)
        {
            case MinigamePhase.Countdown:
                StartGame();
                break;
            case MinigamePhase.Playing:
                Debug.Log($"Minigame {_minigameDefinition.Id}: time is up");
                FinishRound(GetTimeUpStandings());
                break;
        }
    }

    public override void Render()
    {
        if (Phase != MinigamePhase.Countdown && !(Phase == MinigamePhase.Playing && IsTimeLimited))
            return;

        int seconds = Mathf.CeilToInt(TimeRemaining);
        if (seconds == _lastShownSecond || seconds <= 0)
            return;

        _lastShownSecond = seconds;
        if (Phase == MinigamePhase.Countdown)
            OnCountdownTick?.Invoke(seconds);
        else if (seconds <= FINAL_SECONDS)
            OnTimeLimitTick?.Invoke(seconds);
    }

    /// <summary>Host only. Input is gated in the simulation by the networked <see cref="Player.ControlsLocked"/>.</summary>
    protected void SetPlayersInput(bool value)
    {
        foreach (var playerEntry in _playerSpawner.Players)
        {
            if (playerEntry.Value)
                playerEntry.Value.ControlsLocked = !value;
        }
    }

    protected void SetPlayerInput(PlayerRef player, bool value)
    {
        if (_playerSpawner.Players.TryGet(player, out var playerObject) && playerObject)
            playerObject.ControlsLocked = !value;
    }

    private void SetPhase(MinigamePhase phase, float duration = 0f)
    {
        Phase = phase;
        PhaseTimer = duration > 0f ? TickTimer.CreateFromSeconds(Runner, duration) : TickTimer.None;
    }

    private void OnPhaseChanged()
    {
        _lastShownSecond = -1;
        switch (Phase)
        {
            case MinigamePhase.Countdown:
                OnCountdownStarted?.Invoke();
                break;
            case MinigamePhase.Playing:
                OnMinigameStart?.Invoke();
                break;
            case MinigamePhase.Finished:
                OnMinigameEnd?.Invoke();
                break;
        }
    }
}
