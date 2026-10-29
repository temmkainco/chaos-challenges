using Fusion;
using Platform;
using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class Player : NetworkBehaviour, ISpawned
{
    public event Action<string> OnNicknameUpdated;
    [Networked, OnChangedRender(nameof(OnNicknameChanged))] public string Nickname { get; private set; }
    /// <summary>Set by the host (e.g. during a minigame countdown); movement and interaction ignore input while true.</summary>
    [Networked] public NetworkBool ControlsLocked { get; set; }
    /// <summary>Set by the host; the player cannot walk or jump but can still look around and interact (e.g. Pistol Round).</summary>
    [Networked] public NetworkBool MovementLocked { get; set; }
    [field: SerializeField] public CinemachineCamera Camera { get; private set; }
    [field: SerializeField] public PlayerInteraction Interaction { get; private set; }
    [field: SerializeField] public PlayerInput Input { get; private set; }

    private IPlatformService _platformService;

    [Inject]
    public void Construct(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            _platformService = ProjectContext.Instance.Container.Resolve<IPlatformService>();
            InitializePlayer();
        }
    }

    public void SetCameraActive()
    {
        if (Object.HasInputAuthority)
            StartCoroutine(EnableCameraNextFrame());
    }

    private IEnumerator EnableCameraNextFrame()
    {
        yield return null; 
        Camera.gameObject.SetActive(true);
        Camera.Priority = 10;
    }

    public void InitializePlayer()
    {
        string playerNickname = _platformService.GetPlayerName();
        RPC_SetNickname(playerNickname);
        SetCameraActive();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetNickname(string nickname)
    {
        Nickname = nickname;
        OnNicknameUpdated?.Invoke(Nickname); 
    }
    private void OnNicknameChanged()
    {
        OnNicknameUpdated?.Invoke(Nickname);
    }
}