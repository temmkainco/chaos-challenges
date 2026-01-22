using ButtonPack;
using Fusion;
using UnityEngine;

public class LobbyReadyButton : NetworkBehaviour, IInteractable
{
    public Outline Outline { get; private set; }

    public bool CanBeInteractedWith => true;

    private PressController _pressController;

    private void Awake()
    {
        Outline = GetComponent<Outline>();
        Outline.enabled = false;
        _pressController = GetComponent<PressController>();
    }

    public void Interact(PlayerRef player, NetworkObject playerObject)
    {
        _pressController.Toggle1();

        if (!Object.HasStateAuthority) 
            return;
        Debug.Log($"Player {player} pressed the ready button.");
    }
}
