using Core;
using Cysharp.Threading.Tasks;
using Fusion;
using Platform;
using System;
using UnityEngine;
using Zenject;

public class LobbyPlayerSpawner : BasePlayerSpawner, IPlayerJoined, IPlayerLeft
{
    public override void PlayerJoined(PlayerRef player)
    {
        base.PlayerJoined(player);

        Debug.Log($"LobbyPlayerSpawner: Player {player} joined the lobby.");
    }

    public override void PlayerLeft(PlayerRef player)
    {
        base.PlayerLeft(player);

        Debug.Log($"LobbyPlayerSpawner: Player {player} left the lobby.");
    }
}