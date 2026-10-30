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
    private readonly List<PlayerRef> _benched = new();
    private bool _firstPressDone;


    /// <summary>Size chaos does nothing for a turn-based plate game (and giants hide the field).</summary>
    public override bool SupportsChaos(ChaosModifier modifier) =>
        modifier is not (ChaosModifier.Giants or ChaosModifier.Tiny) && base.SupportsChaos(modifier);

    public static readonly string[] Layouts = { "Classic", "Wide Field", "Danger Zone" };
    public override string[] Variants => Layouts;

    public override string TwistTitle => "DOUBLE DARE";
    public override string TwistDescription => "The next contestant has to press two plates.";

    public override void Spawned()
    {
        base.Spawned();
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

        string mines = _minefield.MinesLeft == 1 ? "1 mine left on the field" : $"{_minefield.MinesLeft} mines left on the field";
        return TwistActive ? $"<color=#E03131>Press TWO plates!</color> {mines}" : mines;
    }

    /// <summary>Host only.</summary>
    public void TryPressPlate(PlayerRef player, int index)
    {
        if (!HasStateAuthority || !IsGameActive || player != CurrentTurn)
            return;

        Resolve(index, false);
    }

    protected override void OnTurnTimedOut()
    {
        Debug.Log($"[Mines] {CurrentTurn} ran out of time; pressing a random plate");
        Resolve(_minefield.PickRandomHiddenPlate(), true);
    }

    private void Resolve(int index, bool timedOut)
    {
        var result = _minefield.Reveal(index);
        if (result == PlateState.Hidden)
            return;

        var player = CurrentTurn;
        if (result == PlateState.Mine)
        {
            Debug.Log($"[Mines] {player} hit a mine at plate {index}");
            BlowUp(player, _minefield.GetPlatePosition(index));
            _firstPressDone = false;
            EliminatePlayer(player);
            if (!IsGameActive)
                return;
        }
        else if (TwistActive && !timedOut && !_firstPressDone)
        {
            // Double Dare: the same contestant presses again (the turn clock keeps running).
            _firstPressDone = true;
            Announce("ONE MORE!");
        }
        else
        {
            // A Double Dare covers one turn: once its second plate is pressed, the twist is over.
            if (_firstPressDone && TwistActive) StopChaos();
            _firstPressDone = false;
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

    /// <summary>Host only. Blown-up contestants come back as ghosts around the field, free to move.</summary>
    private void SendToLounge(PlayerRef who)
    {
        if (!_playerSpawner.Players.TryGet(who, out var player) || player == null) return;
        if (!_benched.Contains(who)) _benched.Add(who);
        var flat = Random.insideUnitCircle * 5f;
        ReleaseAsGhost(player, _minefield.transform.position + new Vector3(flat.x, 1.5f, flat.y));
    }

    public override bool HandlePlayerFell(Player player)
    {
        if (!IsGameActive || !_benched.Contains(player.Object.InputAuthority)) return false;
        if (HasStateAuthority) SendToLounge(player.Object.InputAuthority);
        return true;
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
            SendToLounge(player);
        }
    }
}
