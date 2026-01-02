using Fusion;

public interface IGrabbable
{
    bool CanBeGrabbed { get; }
    PlayerRef CurrentHolder { get; }
    void Grab(PlayerRef player);
    void Release(PlayerRef player);
}