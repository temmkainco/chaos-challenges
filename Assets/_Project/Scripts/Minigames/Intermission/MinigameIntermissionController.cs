using Fusion;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class MinigameIntermissionController : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnReadyCountChanged))]
    private int ReadyCount { get; set; }

    [Networked, OnChangedRender(nameof(OnDefinitionReceived))]
    private NetworkString<_128> MinigameId { get; set; }

    [Networked, OnChangedRender(nameof(OnDefinitionReceived))]
    private int MinigameIndex { get; set; }

    private NetworkGameManager _gameManager;
    private bool _localReady;

    public event System.Action<MinigameDefinitionSO> OnDefinitionLoaded;
    public event System.Action<int, int> OnReadyCountUpdated;
    public event System.Action OnAllReady;
    public int TotalPlayers => _gameManager != null ? _gameManager.ExpectedPlayerCount : 0;

    [SerializeField] private CinemachineCamera _matchStartCamera;
    [SerializeField] private float _cameraBlendDuration = 1.5f;
    public MinigameDefinitionSO CurrentDefinition
    {
        get
        {
            if (_gameManager == null)
                _gameManager = FindFirstObjectByType<NetworkGameManager>();
            return _gameManager?.GetDefinitionByIndex(MinigameIndex);
        }
    }

    public override void Spawned()
    {
        _gameManager = FindFirstObjectByType<NetworkGameManager>();

        if (HasStateAuthority)
            MinigameIndex = _gameManager.CurrentMinigameIndex;
        else
            OnDefinitionReceived();
    }
    private void OnDefinitionReceived()
    {
        var def = _gameManager.GetDefinitionByIndex(MinigameIndex);
        if (def != null)
            OnDefinitionLoaded?.Invoke(def);
        else
            Debug.LogWarning($"[Intermission] No definition found for index {MinigameIndex}");

    }
    private void OnReadyCountChanged()
    {
        int total = _gameManager.ExpectedPlayerCount;
        OnReadyCountUpdated?.Invoke(ReadyCount, total);
    }
    public void LocalPlayerReady()
    {
        if (_localReady) return;
        _localReady = true;
        RPC_PlayerReady();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_PlayerReady()
    {
        ReadyCount++;
        int total = _gameManager.ExpectedPlayerCount;

        Debug.Log($"[Intermission] Ready: {ReadyCount}/{total}");

        if (ReadyCount >= total)
        {
            Cursor.visible = false;
            RPC_AllReady();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AllReady()
    {
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