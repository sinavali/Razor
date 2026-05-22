using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Chronos.Abstractions.Shared;

/// <summary>
/// Pure, I/O‑free conversion utilities for bars and tick metrics.
/// Part of the Chronos domain kernel.
/// </summary>
public static class TickSynthesizer
{
    /// <summary>Converts bars into four equidistant synthetic ticks per bar.</summary>
    public static Tick[] BarsToTicks(Bar[] bars)
    {
        if (bars == null || bars.Length == 0) return [];
        int barsCount = bars.Length;
        int totalTicks = barsCount * 4;
        var ticks = GC.AllocateUninitializedArray<Tick>(totalTicks);
        ref Bar barRef = ref MemoryMarshal.GetArrayDataReference(bars);
        ref Tick tickRef = ref MemoryMarshal.GetArrayDataReference(ticks);
        for (int i = 0; i < barsCount; i++)
        {
            ref Bar bar = ref Unsafe.Add(ref barRef, i);
            long openTime = bar.OpenTime;
            long closeTime = bar.CloseTime;
            long duration = closeTime - openTime;
            if (duration <= 0) duration = 4;
            long t2 = openTime + duration / 3;
            long t3 = openTime + duration * 2 / 3;
            long t4 = closeTime > openTime ? closeTime - 1 : openTime + 3;
            double volTick = bar.Volume * 0.25;
            double open = bar.Open, close = bar.Close, high = bar.High, low = bar.Low;
            int tickIdx = i * 4;
            Unsafe.Add(ref tickRef, tickIdx + 0) = new Tick(openTime, open, open, volTick, true);
            Unsafe.Add(ref tickRef, tickIdx + 3) = new Tick(t4, close, close, volTick, true);
            if (close >= open)
            {
                Unsafe.Add(ref tickRef, tickIdx + 1) = new Tick(t2, low, low, volTick, true);
                Unsafe.Add(ref tickRef, tickIdx + 2) = new Tick(t3, high, high, volTick, true);
            }
            else
            {
                Unsafe.Add(ref tickRef, tickIdx + 1) = new Tick(t2, high, high, volTick, true);
                Unsafe.Add(ref tickRef, tickIdx + 2) = new Tick(t3, low, low, volTick, true);
            }
        }

        return ticks;
    }

    /// <summary>Converts bars into a configurable number of synthetic ticks per bar.</summary>
    public static Tick[] BarsToTicks(Bar[] bars, int ticksPerBar, int seed)
    {
        if (bars == null || bars.Length == 0) return [];
        if (ticksPerBar < 2) ticksPerBar = 2;
        var rng = new ChronosRandom(seed);
        int totalTicks = bars.Length * ticksPerBar;
        var ticks = GC.AllocateUninitializedArray<Tick>(totalTicks);
        ref Bar barRef = ref MemoryMarshal.GetArrayDataReference(bars);
        ref Tick tickRef = ref MemoryMarshal.GetArrayDataReference(ticks);
        for (int i = 0; i < bars.Length; i++)
        {
            ref Bar bar = ref Unsafe.Add(ref barRef, i);
            long openTime = bar.OpenTime;
            long closeTime = bar.CloseTime;
            long duration = closeTime - openTime;
            if (duration <= 0) duration = ticksPerBar;
            double volumePerTick = bar.Volume / ticksPerBar;
            double open = bar.Open, close = bar.Close, high = bar.High, low = bar.Low;
            int tickIdx = i * ticksPerBar;
            Unsafe.Add(ref tickRef, tickIdx + 0) = new Tick(openTime, open, open, volumePerTick, true);
            long lastTime = closeTime > openTime ? closeTime - 1 : openTime + ticksPerBar - 1;
            Unsafe.Add(ref tickRef, tickIdx + ticksPerBar - 1) = new Tick(lastTime, close, close, volumePerTick, true);
            for (int t = 1; t < ticksPerBar - 1; t++)
            {
                long tickTime = openTime + (long)((double)t / (ticksPerBar - 1) * duration);
                if (tickTime >= closeTime) tickTime = closeTime - 1;
                double progress = (double)t / (ticksPerBar - 1);
#pragma warning disable CA5394 // Reason: Deterministic use for synthetic tick generation, not security.
                double price = low + (high - low) * (rng.NextDouble() * 0.3 + progress * 0.8);
#pragma warning restore CA5394
                if (price < low) price = low;
                if (price > high) price = high;
                Unsafe.Add(ref tickRef, tickIdx + t) = new Tick(tickTime, price, price, volumePerTick, true);
            }
        }

        return ticks;
    }

    /// <summary>Computes stream metrics: estimated tick count and maximum ticks per stream.</summary>
    public static (int estimatedTicks, int maxTicks) ComputeStreamMetrics(IReadOnlyList<Tick>[] streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        long minTime = long.MaxValue, maxTime = long.MinValue;
        int maxTicks = 0;
        foreach (var s in streams)
        {
            if (s is null) continue;
            if (s.Count > 0)
            {
                if (s[0].Time < minTime) minTime = s[0].Time;
                if (s[^1].Time > maxTime) maxTime = s[^1].Time;
                if (s.Count > maxTicks) maxTicks = s.Count;
            }
        }

        int estimatedTicks = 5000;
        if (minTime != long.MaxValue && maxTime != long.MinValue)
        {
            long estimatedSeconds = (maxTime - minTime) / TimeSpan.TicksPerSecond;
            estimatedTicks = estimatedSeconds > int.MaxValue ? int.MaxValue : (int)estimatedSeconds;
        }

        return (estimatedTicks, maxTicks);
    }
}