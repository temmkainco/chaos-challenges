using Fusion;

public interface ICancellableInteractable : IInteractable
{
    void CancelInteraction(PlayerRef player, NetworkObject playerObject);
}