using Fusion;
using UnityEngine;

public interface IGrabbable
{
    bool CanBeGrabbed { get; }
    PlayerRef CurrentHolder { get; }
    void Grab(PlayerRef player, NetworkObject playerObject);
    void Release(Vector3 force);
}