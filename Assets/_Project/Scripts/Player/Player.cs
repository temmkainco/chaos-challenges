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
    /// <summary>Knocked out of the current round: lives in the lounge, can't touch the game, drawn as a ghost.</summary>
    [Networked] public NetworkBool IsSpectator { get; set; }
    /// <summary>
    /// Chosen look, sent by the owner whenever the player spawns or changes it: part colours as 24-bit RGB
    /// (<see cref="LocalLook.ToRgb"/>), wearables as catalogue indices (<see cref="CosmeticsCatalog"/>).
    /// </summary>
    [Networked, OnChangedRender(nameof(OnLookChanged))] public int BodyRgb { get; private set; }
    [Networked, OnChangedRender(nameof(OnLookChanged))] public int HandsRgb { get; private set; }
    [Networked, OnChangedRender(nameof(OnLookChanged))] public int HeadRgb { get; private set; }
    public Color BodyColor => LocalLook.FromRgb(BodyRgb);
    public Color HandsColor => LocalLook.FromRgb(HandsRgb);
    public Color HeadColor => LocalLook.FromRgb(HeadRgb);
    [Networked, OnChangedRender(nameof(OnLookChanged))] public byte Hat { get; private set; }
    [Networked, OnChangedRender(nameof(OnLookChanged))] public byte Face { get; private set; }
    [Networked, OnChangedRender(nameof(OnLookChanged))] public byte Neck { get; private set; }
    [Networked, OnChangedRender(nameof(OnLookChanged))] public byte Back { get; private set; }
    [field: SerializeField] public CinemachineCamera Camera { get; private set; }
    [field: SerializeField] public PlayerInteraction Interaction { get; private set; }
    [field: SerializeField] public PlayerInput Input { get; private set; }
    [field: SerializeField] public PlayerLook Look { get; private set; }

    /// <summary>The player controlled on this machine, if spawned.</summary>
    public static Player Local { get; private set; }

    /// <summary>
    /// The player's colour (unique in the session, picked in the lobby; see <see cref="NetworkGameManager.PlayerColors"/>):
    /// names, scoreboard swatches, karts, podiums and anything the player owns use it. The outfit's body colour until
    /// the session has given them one.
    /// </summary>
    public Color PlayerColor =>
        NetworkGameManager.Instance is { } match && match.Object != null && match.Object.IsValid && Object != null
        && match.TryGetPlayerColor(Object.InputAuthority, out var color)
            ? color
            : BodyColor;

    private IPlatformService _platformService;

    [Inject]
    public void Construct(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    /// <summary>Current emote (see <see cref="EmotePlayer"/>); 0 = none. Replicated so everyone sees it.</summary>
    [Networked] public byte EmoteId { get; private set; }
    [Networked] public int EmoteTick { get; private set; }

    /// <summary>Sent by the owner's emote wheel.</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Emote(byte emote)
    {
        if (emote >= EmotePlayer.COUNT) return;
        EmoteId = emote;
        EmoteTick = Runner.Tick;
    }

    public override void Spawned()
    {
        if (!TryGetComponent<EmotePlayer>(out _))
            gameObject.AddComponent<EmotePlayer>();
        SpectatorGhost.Attach(this);
        RiderVisual.Attach(this);
        ContestantReveal.Attach(this);
        ColorRing.Attach(this);
        OnLookChanged();

        if (Object.HasInputAuthority)
        {
            Local = this;
            PredictionSmoother.Attach(this);
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
        RPC_SetLook(LocalLook.ToRgb(LocalLook.Body), LocalLook.ToRgb(LocalLook.Hands), LocalLook.ToRgb(LocalLook.Head), (byte)LocalLook.Hat,
            (byte)LocalLook.Face, (byte)LocalLook.Neck, (byte)LocalLook.Back);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetNickname(string nickname)
    {
        Nickname = nickname;
        OnNicknameUpdated?.Invoke(Nickname);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetLook(int body, int hands, int head, byte hat, byte face, byte neck, byte back)
    {
        BodyRgb = body & 0xFFFFFF;
        HandsRgb = hands & 0xFFFFFF;
        HeadRgb = head & 0xFFFFFF;
        Hat = hat;
        Face = face;
        Neck = neck;
        Back = back;
    }

    private void OnNicknameChanged()
    {
        OnNicknameUpdated?.Invoke(Nickname);
    }

    private void OnLookChanged()
    {
        if (Look != null)
            Look.Apply(BodyColor, HandsColor, HeadColor, Hat, Face, Neck, Back);
    }
}
