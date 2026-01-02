using Fusion;

public interface IGrabbable
{
    bool CanBeGrabbed { get; }
    void RPC_RequestGrab(PlayerRef player);
    void RPC_RequestRelease(PlayerRef player);
}
