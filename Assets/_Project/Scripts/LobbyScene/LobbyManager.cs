using Fusion;
using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class LobbyManager : NetworkBehaviour
{
    [Networked, Capacity(6)]
    public NetworkDictionary<PlayerRef, bool> ReadyStates => default;

    [Networked] private TickTimer CountdownTimer { get; set; }
    [Networked] private bool IsCountingDown { get; set; }
    [Networked] private int LastCountdownSecond { get; set; }

    public event Action AllPlayersReadyEvent;
    public event Action<PlayerRef, bool> PlayerReadyChangedEvent;
    public event Action OnCountdownStarted;
    public event Action OnCountdownCancelled;
    public event Action<int> OnCountdownTick;

    [Inject(Id = "NetworkGameManager")]
    private NetworkPrefabRef _networkGameManagerPrefab;

    [Inject] private LobbyPlayerSpawner _spawner;
    [Inject] private MinigameSceneDatabaseSO _minigameSceneDatabase;
    [Inject] private DiContainer _container;

    private NetworkGameManager _networkGameManager;
    private const float COUNTDOWN_DURATION = 10f;

    [SerializeField] private CinemachineCamera _matchStartCamera;
    [SerializeField] private float _cameraBlendDuration = 1.5f;
    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        ResyncWithPlayers();
        _spawner.OnPlayersChangedEvent += ResyncWithPlayers;
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
                SpawnNetworkGameManager();
                StartNetworkGame();
            }
        }
    }

    private void OnDestroy()
    {
        if (_spawner != null)
            _spawner.OnPlayersChangedEvent -= ResyncWithPlayers;
    }

    public void ToggleReady(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        if (!ReadyStates.ContainsKey(player))
            return;

        bool newValue = !ReadyStates[player];
        ReadyStates.Set(player, newValue);
        RPC_NotifyPlayerReadyChanged(player, newValue);
        PlayerReadyChangedEvent?.Invoke(player, newValue);

        CheckAllReady();
    }

    private void ResyncWithPlayers()
    {
        if (!HasStateAuthority)
            return;

        foreach (var playerEntry in _spawner.Players)
        {
            if (!ReadyStates.ContainsKey(playerEntry.Key))
                ReadyStates.Add(playerEntry.Key, false);
        }

        foreach (var readyState in ReadyStates)
        {
            if (!_spawner.Players.ContainsKey(readyState.Key))
                ReadyStates.Remove(readyState.Key);
        }
    }

    private void CheckAllReady()
    {
        if (ReadyStates.Count == 0)
            return;

        bool allReady = true;
        foreach (var readyState in ReadyStates)
        {
            if (!readyState.Value)
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            if (!IsCountingDown)
            {
                StartCountdown();
            }
        }
        else
        {
            if (IsCountingDown)
            {
                CancelCountdown();
            }
        }
    }

    private void StartCountdown()
    {
        IsCountingDown = true;
        CountdownTimer = TickTimer.CreateFromSeconds(Runner, COUNTDOWN_DURATION);
        LastCountdownSecond = Mathf.CeilToInt(COUNTDOWN_DURATION);
        AllPlayersReadyEvent?.Invoke();
        RPC_CountdownStarted();
    }

    private void CancelCountdown()
    {
        IsCountingDown = false;
        RPC_CountdownCancelled();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPlayerReadyChanged(PlayerRef player, bool ready)
    {
        PlayerReadyChangedEvent?.Invoke(player, ready);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CountdownStarted()
    {
        OnCountdownStarted?.Invoke();
        RPC_UpdateCountdown((int)COUNTDOWN_DURATION);
        Debug.Log("Countdown started!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CountdownCancelled()
    {
        OnCountdownCancelled?.Invoke();
        Debug.Log("Countdown cancelled - waiting for all players to be ready");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdateCountdown(int seconds)
    {
        OnCountdownTick?.Invoke(seconds);
        Debug.Log($"Starting in {seconds}...");
    }

    private void SpawnNetworkGameManager()
    {
        if (_networkGameManager != null)
            return;

        var gameFlowControllerObject = Runner.Spawn(
            _networkGameManagerPrefab,
            Vector3.zero,
            Quaternion.identity
        );

        _networkGameManager = gameFlowControllerObject.GetComponent<NetworkGameManager>();
        _container.InjectGameObject(_networkGameManager.gameObject); // ? replaces Initialize
    }

    private void StartNetworkGame()
    {
        if (!HasStateAuthority)
            return;

        foreach (var readyState in ReadyStates)
            ReadyStates.Set(readyState.Key, false);

        RPC_PlayMatchStartCamera();
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayMatchStartCamera()
    {
        StartCoroutine(MatchStartSequence());
    }
    private IEnumerator MatchStartSequence()
    {
        _matchStartCamera.Priority = 20;

        yield return new WaitForSeconds(_cameraBlendDuration);

        if (HasStateAuthority)
            _networkGameManager.StartMatch();
    }

}