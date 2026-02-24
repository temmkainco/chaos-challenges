using ButtonPack;
using Fusion;
using Zenject;

public class LobbyReadyButton : NetworkBehaviour, IInteractable, ILocalInteractable
{
    public Outline Outline { get; private set; }
    public bool CanBeInteractedWith => true;

    private PressController _pressController;

    [Inject] private LobbyManager _lobbyManager;

    private void Awake()
    {
        Outline = GetComponent<Outline>();
        Outline.enabled = false;
        _pressController = GetComponent<PressController>();
    }
    public void LocalInteract()
    {
        _pressController.Toggle1();
    }

    public void Interact(PlayerRef player, NetworkObject playerObject)
    {
        if (!Object.HasStateAuthority)
            return;

        _lobbyManager.ToggleReady(player);
    }
}
