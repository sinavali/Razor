namespace Chronos.Samples.Plugins.Strategies.CfdSmtDivergence;

/// <summary>
/// Checks whether the current UTC time falls within one or more custom work‑time windows.
/// Each window can be individually enabled via a gene.
/// </summary>
internal sealed class WorkTimeFilter
{
    private static readonly (string Name, TimeSpan Start, TimeSpan End)[] WorkTimes =
    {
        ("London WT1", new TimeSpan(5, 45, 0),  new TimeSpan(8,  0, 0)),
        ("London WT2", new TimeSpan(8, 45, 0),  new TimeSpan(9, 30, 0)),
        ("NY WT1",     new TimeSpan(10, 45, 0), new TimeSpan(13, 0, 0)),
        ("NY WT2",     new TimeSpan(13, 45, 0), new TimeSpan(14, 30, 0)),
        ("NY WT3",     new TimeSpan(16, 45, 0), new TimeSpan(18, 0, 0))
    };

    /// <summary>Gene: enable London WT1.</summary>
    public bool UseLWT1 { get; set; } = true;
    /// <summary>Gene: enable London WT2.</summary>
    public bool UseLWT2 { get; set; } = true;
    /// <summary>Gene: enable NY WT1.</summary>
    public bool UseNYWT1 { get; set; } = true;
    /// <summary>Gene: enable NY WT2.</summary>
    public bool UseNYWT2 { get; set; } = true;
    /// <summary>Gene: enable NY WT3.</summary>
    public bool UseNYWT3 { get; set; } = true;

    /// <summary>
    /// Returns 1 if the tick time falls within at least one enabled work‑time window, 0 otherwise.
    /// </summary>
    /// <param name="tickTime">The tick timestamp in UTC ticks.</param>
    /// <returns>1 if in work time, 0 otherwise.</returns>
    public int IsInWorkTime(long tickTime)
    {
        TimeSpan timeOfDay = new DateTime(tickTime, DateTimeKind.Utc).TimeOfDay;
        bool[] enabled = { UseLWT1, UseLWT2, UseNYWT1, UseNYWT2, UseNYWT3 };
        for (int i = 0; i < WorkTimes.Length; i++)
        {
            if (!enabled[i]) continue;
            var (_, start, end) = WorkTimes[i];
            if (timeOfDay >= start && timeOfDay <= end)
                return 1;
        }
        return 0;
    }
}
