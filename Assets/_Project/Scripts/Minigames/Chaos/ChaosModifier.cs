using UnityEngine;

/// <summary>
/// Buffs, debuffs and hazards that can be thrown at a running minigame. This is the GDD's Streamer Mode hook:
/// today a random director picks them (host setting); later the website vote will call
/// <see cref="ChaosDirector.Request"/> on the host with the winning modifier. Every minigame supports the shared
/// ones and may add its own twist in <see cref="MinigameController.OnChaosStarted"/>.
/// </summary>
public enum ChaosModifier : byte
{
    None,
    LowGravity,
    SpeedRush,
    SlowMotion,
    Giants,
    Tiny,
    ReversedControls,
    IceFloor,
    SuperJump,
    MeteorShower,
    Blackout,
    /// <summary>The running minigame's own twist (<see cref="MinigameController.TwistTitle"/>): broadsides, a master key...</summary>
    Twist,
}

public enum ChaosLevel : byte
{
    Off,
    Normal,
    Wild,
}

public static class ChaosInfo
{
    public static string Title(ChaosModifier m) => m switch
    {
        ChaosModifier.LowGravity => "LOW GRAVITY",
        ChaosModifier.SpeedRush => "SPEED RUSH",
        ChaosModifier.SlowMotion => "SLOW MOTION",
        ChaosModifier.Giants => "GIANTS",
        ChaosModifier.Tiny => "TINY TROUBLE",
        ChaosModifier.ReversedControls => "REVERSED CONTROLS",
        ChaosModifier.IceFloor => "ICE FLOOR",
        ChaosModifier.SuperJump => "SUPER JUMP",
        ChaosModifier.MeteorShower => "METEOR SHOWER",
        ChaosModifier.Blackout => "BLACKOUT",
        ChaosModifier.Twist => MinigameController.Current != null ? MinigameController.Current.TwistTitle ?? "TWIST" : "TWIST",
        _ => "",
    };

    /// <summary>Buff (helps everyone move), debuff (hinders) or hazard (can knock you out).</summary>
    public static string Kind(ChaosModifier m) => m switch
    {
        ChaosModifier.LowGravity or ChaosModifier.SpeedRush or ChaosModifier.SuperJump or ChaosModifier.Giants => "BUFF",
        ChaosModifier.MeteorShower => "HAZARD",
        ChaosModifier.Twist => MinigameController.Current != null ? MinigameController.Current.TwistKind : "HAZARD",
        ChaosModifier.None => "",
        _ => "DEBUFF",
    };

    public static Color KindColor(ChaosModifier m) => Kind(m) switch
    {
        "BUFF" => new Color(0.31f, 0.82f, 0.63f),
        "HAZARD" => new Color(1f, 0.37f, 0.36f),
        _ => new Color(0.72f, 0.61f, 0.95f),
    };

    public const int COUNT = 12;
}

/// <summary>
/// Host-side entry point for chaos. The future web/streamer API only needs to call <see cref="Request"/> on the host.
/// </summary>
public static class ChaosDirector
{
    /// <summary>Set by the host's match settings; Off disables the random director (requests still work).</summary>
    public static ChaosLevel Level = ChaosLevel.Off;

    /// <summary>Host only. Starts <paramref name="modifier"/> in the running minigame. Returns false if it cannot run now.</summary>
    public static bool Request(ChaosModifier modifier, float seconds = 12f)
    {
        var minigame = MinigameController.Current;
        return minigame != null && minigame.StartChaos(modifier, seconds);
    }
}
