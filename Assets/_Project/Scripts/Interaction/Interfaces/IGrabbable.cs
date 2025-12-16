using Fusion;

public interface IGrabbable
{
    bool CanBeGrabbed { get; }
    void RequestGrab(PlayerRef player);
    void RequestRelease(PlayerRef player);
}
