using ButtonPack;
using Fusion;
using Zenject;

public class LobbyReadyButton : NetworkBehaviour, IInteractable, ILocalInteractable
{
    public Outline Outline { get; private set; }
    public bool CanBeInteractedWith => true;

    private PressController _pressController;
    private bool _shownPressed;

    [Inject] private LobbyManager _lobbyManager;

    private void Awake()
    {
        Outline = GetComponent<Outline>();
        Outline.enabled = false;
        _pressController = GetComponent<PressController>();
    }
    /// <summary>
    /// The button shows the local player's replicated ready state. It used to flip on every local press, which
    /// drifted from the host's state (a press the host didn't register, lag), so it looked pressed when you weren't ready.
    /// </summary>
    public void LocalInteract() { }

    private void Update()
    {
        if (_lobbyManager == null || _lobbyManager.Object == null || !_lobbyManager.Object.IsValid || _pressController == null) return;
        var local = _lobbyManager.Runner.LocalPlayer;
        bool ready = _lobbyManager.ReadyStates.TryGet(local, out var value) && value;
        if (ready == _shownPressed) return;
        _shownPressed = ready;
        _pressController.Toggle1();
    }

    public void Interact(PlayerRef player, NetworkObject playerObject)
    {
        if (!Object.HasStateAuthority)
            return;

        _lobbyManager.ToggleReady(player);
    }
}
