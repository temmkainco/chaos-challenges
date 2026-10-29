using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

public class MinigameIntermissionController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnReadyCountChanged))]
    private int ReadyCount { get; set; }

    [Networked, OnChangedRender(nameof(OnDefinitionReceived))]
    private int MinigameIndex { get; set; }

    /// <summary>The round starts when this runs out even if not everyone pressed ready (AFK players).</summary>
    [Networked] public TickTimer ReadyTimer { get; private set; }

    private NetworkGameManager _gameManager;
    private bool _localReady;
    private bool _proceeding;
    private readonly HashSet<PlayerRef> _readyPlayers = new();

    public event System.Action<MinigameDefinitionSO> OnDefinitionLoaded;
    public event System.Action<int, int> OnReadyCountUpdated;
    public event System.Action OnAllReady;
    public int TotalPlayers => _gameManager != null ? _gameManager.ConnectedPlayerCount : 0;

    [SerializeField] private CinemachineCamera _matchStartCamera;
    [SerializeField] private float _cameraBlendDuration = 1.5f;
    [SerializeField] private float _readyTimeout = 30f;

    public float ReadyTimeRemaining => ReadyTimer.RemainingTime(Runner) ?? 0f;

    public MinigameDefinitionSO CurrentDefinition => _gameManager?.GetDefinitionByIndex(MinigameIndex);

    public override void Spawned()
    {
        _gameManager = NetworkGameManager.Instance;

        if (HasStateAuthority)
        {
            MinigameIndex = _gameManager.CurrentMinigameIndex;
            ReadyTimer = TickTimer.CreateFromSeconds(Runner, _readyTimeout);
        }
        else
            OnDefinitionReceived();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || _proceeding || !ReadyTimer.Expired(Runner))
            return;

        Debug.Log($"[Intermission] Ready timeout: starting with {ReadyCount}/{TotalPlayers} ready");
        Proceed();
    }

    private void OnDefinitionReceived()
    {
        var def = CurrentDefinition;
        if (def != null)
            OnDefinitionLoaded?.Invoke(def);
        else
            Debug.LogWarning($"[Intermission] No definition found for index {MinigameIndex}");
    }

    private void OnReadyCountChanged()
    {
        OnReadyCountUpdated?.Invoke(ReadyCount, TotalPlayers);
    }

    public void LocalPlayerReady()
    {
        if (_localReady) return;
        _localReady = true;
        RPC_PlayerReady();
    }

    /// <summary>Host only, called by <see cref="NetworkGameManager"/>.</summary>
    public void HandlePlayerLeft(PlayerRef player)
    {
        _readyPlayers.Remove(player);
        RecountReady();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_PlayerReady(RpcInfo info = default)
    {
        // A local invoke (the host readying up) carries no source player.
        var player = info.IsInvokeLocal ? Runner.LocalPlayer : info.Source;
        if (_readyPlayers.Add(player))
            RecountReady();
    }

    private void RecountReady()
    {
        ReadyCount = _readyPlayers.Count(_gameManager.IsConnectedPlayer);
        Debug.Log($"[Intermission] Ready: {ReadyCount}/{TotalPlayers}");

        if (!_proceeding && TotalPlayers > 0 && ReadyCount >= TotalPlayers)
            Proceed();
    }

    private void Proceed()
    {
        _proceeding = true;
        ReadyTimer = TickTimer.None;
        RPC_AllReady();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AllReady()
    {
        Cursor.visible = false;
        OnAllReady?.Invoke();
        StartCoroutine(MatchStartSequence());
    }

    private IEnumerator MatchStartSequence()
    {
        _matchStartCamera.Priority = 20;

        yield return new WaitForSeconds(_cameraBlendDuration);

        if (HasStateAuthority)
            _gameManager.ProceedFromIntermission();
    }
}
