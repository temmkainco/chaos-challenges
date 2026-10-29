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
    /// <summary>Chosen look (see <see cref="CosmeticsCatalog"/>), sent by the owner whenever the player spawns.</summary>
    [Networked, OnChangedRender(nameof(OnLookChanged))] public byte BodyColor { get; private set; }
    [Networked, OnChangedRender(nameof(OnLookChanged))] public byte HandsColor { get; private set; }
    [Networked, OnChangedRender(nameof(OnLookChanged))] public byte HeadColor { get; private set; }
    [Networked, OnChangedRender(nameof(OnLookChanged))] public byte Hat { get; private set; }
    [field: SerializeField] public CinemachineCamera Camera { get; private set; }
    [field: SerializeField] public PlayerInteraction Interaction { get; private set; }
    [field: SerializeField] public PlayerInput Input { get; private set; }
    [field: SerializeField] public PlayerLook Look { get; private set; }

    /// <summary>The player controlled on this machine, if spawned.</summary>
    public static Player Local { get; private set; }

    /// <summary>Body colour, used to tint the player's name in the UI.</summary>
    public Color OutfitColor => CosmeticsCatalog.Instance != null ? CosmeticsCatalog.Instance.GetColor(BodyColor).Color : Color.white;

    private IPlatformService _platformService;

    [Inject]
    public void Construct(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public override void Spawned()
    {
        OnLookChanged();

        if (Object.HasInputAuthority)
        {
            Local = this;
            _platformService = ProjectContext.Instance.Container.Resolve<IPlatformService>();
            LocalLook.Changed += SendLocalLook;
            InitializePlayer();
            CameraSensitivity.Attach(this);
        }
        else
        {
            Nameplate.Attach(this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this)
        {
            Local = null;
            LocalLook.Changed -= SendLocalLook;
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
        SendLocalLook();
        SetCameraActive();
    }

    private void SendLocalLook()
    {
        RPC_SetLook((byte)LocalLook.Body, (byte)LocalLook.Hands, (byte)LocalLook.Head, (byte)LocalLook.Hat);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetNickname(string nickname)
    {
        Nickname = nickname;
        OnNicknameUpdated?.Invoke(Nickname);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetLook(byte body, byte hands, byte head, byte hat)
    {
        BodyColor = body;
        HandsColor = hands;
        HeadColor = head;
        Hat = hat;
    }

    private void OnNicknameChanged()
    {
        OnNicknameUpdated?.Invoke(Nickname);
    }

    private void OnLookChanged()
    {
        if (Look != null)
            Look.Apply(BodyColor, HandsColor, HeadColor, Hat);
    }
}
