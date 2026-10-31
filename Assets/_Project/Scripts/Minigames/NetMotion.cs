using Fusion;
using UnityEngine;

/// <summary>
/// A value moving at a steady rate since a tick: a sweeper's angle, a log's roll, rising lava. Networked as one
/// struct, so every peer can work out the value at any moment instead of showing the last update it received:
/// <see cref="Render"/> poses the visual at the local render time (smooth, and in step with your own predicted
/// player), and the simulation uses <see cref="AtTick"/>, so the host and a predicting client agree on where it was
/// at every tick. The host re-anchors it with <see cref="Retarget"/> whenever the rate changes.
/// </summary>
public struct NetMotion : INetworkStruct
{
    public float Value;
    /// <summary>Units per second.</summary>
    public float Rate;
    public int Tick;

    public float At(NetworkRunner runner, float time) => Value + Rate * (time - Tick * runner.DeltaTime);

    /// <summary>Where it is at the tick being simulated.</summary>
    public float AtTick(NetworkRunner runner) => At(runner, runner.Tick * runner.DeltaTime);

    /// <summary>Where to draw it this frame (the local render time: in step with the predicted local player).</summary>
    public float Render(NetworkRunner runner) => At(runner, runner.LocalRenderTime);

    /// <summary>Host. Continues from where it is now at a new rate (and wraps angles to keep the numbers small).</summary>
    public NetMotion Retarget(NetworkRunner runner, float rate, float wrap = 0f)
    {
        float now = AtTick(runner);
        return new NetMotion { Value = wrap > 0f ? Mathf.Repeat(now, wrap) : now, Rate = rate, Tick = runner.Tick };
    }

    public static NetMotion Still(NetworkRunner runner, float value) => new() { Value = value, Rate = 0f, Tick = runner.Tick };
}
