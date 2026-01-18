using Fusion;
using UnityEngine;

public readonly struct FocusContext
{
    public readonly Transform ViewOrigin;
    public readonly PlayerRef Player;

    public FocusContext(Transform viewOrigin, PlayerRef player)
    {
        ViewOrigin = viewOrigin;
        Player = player;
    }
}
