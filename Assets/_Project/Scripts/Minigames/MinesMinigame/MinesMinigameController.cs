// MinesMinigameController.cs
using Fusion;
using UnityEngine;
using Zenject;

public class MinesMinigameController : TurnBasedMinigameController
{
    [Inject] private Minefield _minefield;

    protected override void StartGame()
    {
        base.StartGame();

        if (!HasStateAuthority)
            return;

        _minefield.Initialize(this);
        _minefield.Generate(_alivePlayers.Count);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ResolveTile(int x, int y)
    {
        if (!IsGameActive) return;

        RPC_PressButton(x, y);

        if (_minefield.IsMine(x, y))
        {
            Debug.Log($"Player {CurrentTurn} hit a mine at ({x}, {y}) and is eliminated!");
            EliminatePlayer(CurrentTurn);
        }
        else
        {
            AdvanceTurn();
            Debug.Log($"Next turn: {CurrentTurn}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PressButton(int x, int y)
    {
        _minefield.PressButton(x, y);
    }

    protected override void EndGame()
    {
        base.EndGame();
        PlayerRef winner = _alivePlayers.Count > 0 ? _alivePlayers[0] : PlayerRef.None;
        Debug.Log($"Game Over! Winner: {winner}");
    }
}