using Fusion;
using Platform;
using System;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class Player : NetworkBehaviour, ISpawned
{
    public event Action<string> OnNicknameUpdated;
    [Networked, OnChangedRender(nameof(OnNicknameChanged))] public string Nickname { get; private set; }

    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private PlayerInput _input;

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
            _camera.gameObject.SetActive(true);
            _platformService = ProjectContext.Instance.Container.Resolve<IPlatformService>();
            InitializePlayer();
        }
    }

    public void InitializePlayer()
    {
        string playerNickname = _platformService.GetPlayerName();
        RPC_SetNickname(playerNickname);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetNickname(string nickname)
    {
        Nickname = nickname;
    }
    private void OnNicknameChanged()
    {
        OnNicknameUpdated?.Invoke(Nickname);
    }
}