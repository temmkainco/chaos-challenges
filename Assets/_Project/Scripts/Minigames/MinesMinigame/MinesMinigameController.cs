using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Mines Round: players take turns pressing plates. A safe plate passes the turn; a mine blows the player off
/// the stage and out of the round. Running out of time presses a random plate for you. When only mines are
/// left, a fresh field is dealt. The last player standing wins.
/// </summary>
public class MinesMinigameController : TurnBasedMinigameController
{
    [Inject] private Minefield _minefield;

    [Header("Explosion")]
    [SerializeField] private float _launchUp = 12f;
    [SerializeField] private float _launchOut = 10f;
    [SerializeField, Tooltip("Seconds before a blown-up player is put on the side lines")] private float _benchDelay = 2.5f;

    /// <summary>True on the host (it checks turns itself) and for the local player during their turn.</summary>
    public bool IsLocalTurnOrHost => HasStateAuthority || CurrentTurn == Runner.LocalPlayer;

    private readonly List<(PlayerRef player, TickTimer timer)> _pendingBench = new();

    public override void Spawned()
    {
        _minefield.Initialize(this);
    }

    protected override void StartGame()
    {
        base.StartGame();

        if (!HasStateAuthority)
            return;

        _minefield.Generate(_alivePlayers.Count);
    }

    public override string GetStatusText()
    {
        if (_minefield.Object == null || !_minefield.Object.IsValid || _minefield.Size == 0)
            return null;

        return _minefield.MinesLeft == 1 ? "1 mine left on the field" : $"{_minefield.MinesLeft} mines left on the field";
    }

    /// <summary>Host only.</summary>
    public void TryPressPlate(PlayerRef player, int index)
    {
        if (!HasStateAuthority || !IsGameActive || player != CurrentTurn)
            return;

        Resolve(index);
    }

    protected override void OnTurnTimedOut()
    {
        Debug.Log($"[Mines] {CurrentTurn} ran out of time; pressing a random plate");
        Resolve(_minefield.PickRandomHiddenPlate());
    }

    private void Resolve(int index)
    {
        var result = _minefield.Reveal(index);
        if (result == PlateState.Hidden)
            return;

        var player = CurrentTurn;
        if (result == PlateState.Mine)
        {
            Debug.Log($"[Mines] {player} hit a mine at plate {index}");
            BlowUp(player, _minefield.GetPlatePosition(index));
            EliminatePlayer(player);
            if (!IsGameActive)
                return;
        }
        else
        {
            AdvanceTurn();
        }

        if (!_minefield.HasHiddenSafePlate())
        {
            Debug.Log("[Mines] Only mines left; dealing a new field");
            _minefield.Generate(_alivePlayers.Count);
        }
    }

    private void BlowUp(PlayerRef player, Vector3 platePosition)
    {
        if (!_playerSpawner.Players.TryGet(player, out var playerObject) || playerObject == null)
            return;

        var away = playerObject.transform.position - platePosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = Random.insideUnitCircle.normalized;
        away = new Vector3(away.x, 0f, away.z).normalized;

        if (playerObject.TryGetComponent<PlayerMovement>(out var movement))
            movement.Launch(away * _launchOut + Vector3.up * _launchUp);

        _pendingBench.Add((player, TickTimer.CreateFromSeconds(Runner, _benchDelay)));
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority)
            return;

        for (int i = _pendingBench.Count - 1; i >= 0; i--)
        {
            var (player, timer) = _pendingBench[i];
            if (!timer.Expired(Runner))
                continue;

            _pendingBench.RemoveAt(i);
            if (_playerSpawner.Players.TryGet(player, out var playerObject) && playerObject != null)
                _playerSpawner.RequestSpawn(playerObject);
        }
    }
}
