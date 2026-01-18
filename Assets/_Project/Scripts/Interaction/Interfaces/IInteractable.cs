using Fusion;
public interface IInteractable
{
    Outline Outline { get; }    
    bool CanBeInteractedWith { get; }
    void Interact(PlayerRef player, NetworkObject playerObject);
}