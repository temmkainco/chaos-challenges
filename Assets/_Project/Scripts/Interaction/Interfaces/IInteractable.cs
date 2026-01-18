using Fusion;

public interface IInteractable
{
    bool CanBeInteractedWith { get; }
    void Interact(PlayerRef player, NetworkObject playerObject);
}