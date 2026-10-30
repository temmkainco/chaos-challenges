using System;
using UnityEngine;

/// <summary>
/// Development probe: records frame time and managed allocations per frame (main thread) so automated test runs can
/// report hitches and garbage. Added at runtime by the test tooling; not part of any scene.
/// </summary>
public class FrameStats : MonoBehaviour
{
    private const int CAPACITY = 600;

    private readonly float[] _frameMs = new float[CAPACITY];
    private readonly long[] _allocBytes = new long[CAPACITY];
    private int _count;
    private long _lastAllocated;

    private void OnEnable()
    {
        _count = 0;
        _lastAllocated = GC.GetAllocatedBytesForCurrentThread();
    }

    private void Update()
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        if (_count < CAPACITY)
        {
            _frameMs[_count] = Time.unscaledDeltaTime * 1000f;
            _allocBytes[_count] = allocated - _lastAllocated;
            _count++;
        }
        _lastAllocated = allocated;
    }

    public string Summary()
    {
        if (_count == 0) return "no frames";
        double ms = 0, worst = 0; long bytes = 0, worstBytes = 0; int hitches = 0;
        for (int i = 1; i < _count; i++)   // skip the first, partial frame
        {
            ms += _frameMs[i]; worst = Math.Max(worst, _frameMs[i]);
            bytes += _allocBytes[i]; worstBytes = Math.Max(worstBytes, _allocBytes[i]);
            if (_frameMs[i] > 50f) hitches++;
        }
        int n = Math.Max(1, _count - 1);
        return $"frames={n} avg={ms / n:F1}ms worst={worst:F1}ms hitches>50ms={hitches} alloc/frame avg={bytes / n}B worst={worstBytes}B";
    }
}
