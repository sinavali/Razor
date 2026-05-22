using Chronos.Abstractions.Shared;

namespace Chronos.Kernel.Backtesting;

/// <summary>
/// Merges multiple tick sources into a single time‑ordered, deterministic stream.
/// </summary>
public static class MergedTickTimeline
{
    /// <summary>Yields ticks in chronological order across all streams.</summary>
    /// <param name="streams">Sorted tick streams.</param>
    /// <param name="symbols">Symbol names (not used by the merge, passed for context).</param>
    public static IEnumerable<(long Time, int StreamIndex, Tick Tick)> EnumerateEvents(IReadOnlyList<Tick>[] streams, string[] symbols)
    {
        ArgumentNullException.ThrowIfNull(streams);
        int streamCount = streams.Length;
        if (streamCount == 0) yield break;

        var enumerators = new IEnumerator<Tick>[streamCount];
        var comparer = Comparer<(long Time, int Index)>.Create((a, b) =>
        {
            int cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });
        var queue = new PriorityQueue<int, (long, int)>(comparer);

        for (int i = 0; i < streamCount; i++)
        {
            var e = streams[i].GetEnumerator();
            enumerators[i] = e;
            if (e.MoveNext()) queue.Enqueue(i, (e.Current.Time, i));
        }

        while (queue.TryDequeue(out int idx, out _))
        {
            var e = enumerators[idx];
            yield return (e.Current.Time, idx, e.Current);
            if (e.MoveNext()) queue.Enqueue(idx, (e.Current.Time, idx));
        }
    }
}