using UnityEngine;

/// <summary>
/// Every value of the shove (the empty-handed throw button) in one place: Resources/PushConfig. Targets are picked in a
/// cone in front of the character (its facing, never the camera), the push lands at the contact moment of the
/// swing, and a shoved contestant can't be shoved again straight away.
/// </summary>
[CreateAssetMenu(menuName = "Game/Push Config")]
public class PushConfig : ScriptableObject
{
    [Header("Reach")]
    [Tooltip("Metres from the shover's centre to the target's body (the target's capsule radius is added)")]
    public float Range = 1.5f;
    [Tooltip("Half-angle of the cone in front of the character (degrees)")]
    public float ConeAngle = 45f;
    [Tooltip("Targets more than this far above or below the shover are out of reach (m)")]
    public float MaxHeightDifference = 1.5f;
    [Tooltip("Angles closer than this (degrees) count as equal; the nearer target wins")]
    public float AngleTie = 8f;
    [Tooltip("What blocks a shove (walls, props): contestants and the invisible play-area walls are left out")]
    public LayerMask LineOfSight = ~((1 << ArenaProps.PLAYER_LAYER) | (1 << ArenaProps.FENCE_LAYER));

    [Header("Timing")]
    [Tooltip("Seconds from the press to the contact frame of the swing (Shove.anim's arms-out key), when the push lands")]
    public float ContactDelay = 0.16f;
    [Tooltip("Seconds from one shove to the next (counted from the press)")]
    public float Cooldown = 0.9f;

    [Header("Knockback")]
    [Tooltip("Horizontal speed given to the target (m/s), times the minigame's ShoveStrength")]
    public float Force = 9f;
    [Tooltip("Upward speed given to the target (m/s)")]
    public float Lift = 3.5f;
    [Tooltip("Seconds the target has little control (it ends early once they land, after a quarter second)")]
    public float ControlLoss = 0.6f;
    [Tooltip("Seconds after being shoved during which a contestant can't be shoved again (no chain stuns)")]
    public float Immunity = 0.8f;

    [Header("Debug")]
    [Tooltip("Draw every contestant's shove range and cone in the Scene view")]
    public bool DrawGizmos = true;

    private static PushConfig _instance;

    /// <summary>The config in Resources (defaults if the asset is missing).</summary>
    public static PushConfig Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<PushConfig>("PushConfig");
            if (_instance == null) _instance = CreateInstance<PushConfig>();
            return _instance;
        }
    }
}
