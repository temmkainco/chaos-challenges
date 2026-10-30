using UnityEngine;

/// <summary>
/// How the host wants the show to run, chosen on the host panel and saved for next time. Only the host reads these
/// (it runs the match); clients see the results through replicated state (round count, chaos, timers).
/// </summary>
public static class MatchSettings
{
    public const int MIN_ROUNDS = 1, MAX_ROUNDS = 10, DEFAULT_ROUNDS = 3;

    public enum Length : byte { Short, Normal, Long }

    /// <summary>Minigames per match, picked at random without repeats.</summary>
    public static int Rounds
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("match.rounds", DEFAULT_ROUNDS), MIN_ROUNDS, MAX_ROUNDS);
        set { PlayerPrefs.SetInt("match.rounds", Mathf.Clamp(value, MIN_ROUNDS, MAX_ROUNDS)); PlayerPrefs.Save(); }
    }

    public static Length RoundLength
    {
        get => (Length)Mathf.Clamp(PlayerPrefs.GetInt("match.length", (int)Length.Normal), 0, 2);
        set { PlayerPrefs.SetInt("match.length", (int)value); PlayerPrefs.Save(); }
    }

    /// <summary>Multiplier on every time-limited round.</summary>
    public static float LengthMultiplier => RoundLength switch { Length.Short => 0.7f, Length.Long => 1.4f, _ => 1f };

    public static ChaosLevel Chaos
    {
        get => (ChaosLevel)Mathf.Clamp(PlayerPrefs.GetInt("match.chaos", (int)ChaosLevel.Normal), 0, 2);
        set { PlayerPrefs.SetInt("match.chaos", (int)value); PlayerPrefs.Save(); }
    }

    public static bool Shoving
    {
        get => PlayerPrefs.GetInt("match.shove", 1) == 1;
        set { PlayerPrefs.SetInt("match.shove", value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
