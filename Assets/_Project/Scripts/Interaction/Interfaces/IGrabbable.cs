using Fusion;
using UnityEngine;

public interface IGrabbable
{
    bool CanBeGrabbed { get; }
    PlayerRef CurrentHolder { get; }
    void Grab(PlayerRef player, Transform holdPoint);
    void Release(PlayerRef player);
}