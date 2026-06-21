using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Backtesting;

/// <summary>
/// Merges multiple tick sources into a single time‑ordered, deterministic stream.
/// </summary>
public static class MergedTickTimeline
{
    /// <summary>Yields ticks in chronological order across all streams.</summary>
    public static IEnumerable<(long Time, int StreamIndex, Tick Tick)> EnumerateEvents(
        IReadOnlyList<Tick>[] streams, string[] symbols)
    {
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(symbols);

        if (streams.Length != symbols.Length)
        {
            throw new ArgumentException(
                $"Streams count ({streams.Length}) must equal symbols count ({symbols.Length}).");
        }

        int streamCount = streams.Length;
        if (streamCount == 0)
        {
            yield break;
        }

        var enumerators = new IEnumerator<Tick>[streamCount];
        var comparer = Comparer<(long Time, int Index)>.Create((a, b) =>
        {
            int cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        var queue = new PriorityQueue<int, (long, int)>(comparer);
        var lastTimePerStream = new long[streamCount];

        for (int i = 0; i < streamCount; i++)
        {
            lastTimePerStream[i] = long.MinValue;

            if (streams[i] is null)
            {
                continue;
            }

            var e = streams[i].GetEnumerator();
            enumerators[i] = e;
            if (e.MoveNext())
            {
                queue.Enqueue(i, (e.Current.Time, i));
            }
        }

        while (queue.TryDequeue(out int idx, out _))
        {
            var e = enumerators[idx];
            var currentTick = e.Current;

            // Enforce sorted invariant per Principle 8
            if (currentTick.Time < lastTimePerStream[idx])
            {
                throw new InvalidOperationException(
                    $"Stream {idx} ({symbols[idx]}) contains unsorted ticks: " +
                    $"{currentTick.Time} < {lastTimePerStream[idx]}");
            }

            lastTimePerStream[idx] = currentTick.Time;

            yield return (currentTick.Time, idx, currentTick);

            if (e.MoveNext())
            {
                queue.Enqueue(idx, (e.Current.Time, idx));
            }
        }
    }
}
