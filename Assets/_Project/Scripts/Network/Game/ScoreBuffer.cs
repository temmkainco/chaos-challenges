using System.Collections.Generic;
using Fusion;

/// <summary>
/// Host-only in-memory buffer that collects per-slot scores from RPCs
/// before they are flushed to NetworkGameManager. Keyed by NetworkObject ID
/// so it survives if multiple minigame controllers exist simultaneously.
/// </summary>
public static class ScoreBuffer
{
    private static readonly Dictionary<NetworkId, int[]> _buffers = new();

    public static void Record(NetworkId id, int slot, int score)
    {
        if (!_buffers.TryGetValue(id, out var arr))
        {
            arr = new int[8]; // max players
            _buffers[id] = arr;
        }
        arr[slot] += score;
    }

    /// <summary>Returns a copy of the buffer and removes it from memory.</summary>
    public static int[] FlushAndGet(NetworkId id, int playerCount)
    {
        if (!_buffers.TryGetValue(id, out var arr))
            return new int[playerCount];

        _buffers.Remove(id);
        var result = new int[playerCount];
        for (int i = 0; i < playerCount; i++)
            result[i] = arr[i];
        return result;
    }
}