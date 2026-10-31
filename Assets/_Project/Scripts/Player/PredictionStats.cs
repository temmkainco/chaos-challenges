using Fusion;
using UnityEngine;

/// <summary>
/// Measures real mispredictions of your own contestant on a client: where prediction put you at a tick, against where
/// you are at that same tick once the host's data for it has arrived and the tick has been re-simulated. Walls and
/// fences blocking you are not counted (that is prediction agreeing with the host), nor are teleports (respawns, the
/// lounge: over 3 m). One count per frame, at its largest error, when that is over 2 cm. Shown by the F3 overlay
/// (<see cref="Networking.NetStatsOverlay"/>).
/// </summary>
public static class PredictionStats
{
    private const int SIZE = 128;
    private const float MIN_ERROR = 0.02f, TELEPORT = 3f;

    public static int Mispredictions;
    public static float MispredictedMetres;

    private static readonly Vector3[] Predicted = new Vector3[SIZE];
    private static readonly int[] PredictedTick = new int[SIZE];
    private static int _frame = -1;
    private static float _frameError;

    /// <summary>Client, local contestant, after simulating each tick (forward or re-simulated).</summary>
    public static void Track(NetworkRunner runner, Vector3 position)
    {
        if (Time.frameCount != _frame)
        {
            if (_frameError > MIN_ERROR)
            {
                Mispredictions++;
                MispredictedMetres += _frameError;
            }
            _frameError = 0f;
            _frame = Time.frameCount;
        }
        int i = runner.Tick & (SIZE - 1);
        if (runner.IsResimulation && PredictedTick[i] == runner.Tick)
        {
            float error = (position - Predicted[i]).magnitude;
            if (error < TELEPORT) _frameError = Mathf.Max(_frameError, error);
        }
        Predicted[i] = position;
        PredictedTick[i] = runner.Tick;
    }
}
