namespace Chronos.Samples.Plugins.Strategies.CfdSmtDivergence;

/// <summary>
/// Checks whether the current UTC time falls within one or more trading sessions.
/// Each session is a fixed UTC time window and can be individually enabled via a gene.
/// </summary>
internal sealed class SessionFilter
{
    private static readonly (string Name, TimeSpan Start, TimeSpan End)[] Sessions =
    {
        ("Asia",       new TimeSpan(0,  0, 0),  new TimeSpan(5, 30, 0)),
        ("London",     new TimeSpan(5, 45, 0),  new TimeSpan(10, 0, 0)),
        ("NewYork 1",  new TimeSpan(10, 45, 0), new TimeSpan(16, 0, 0)),
        ("NewYork 2",  new TimeSpan(16, 45, 0), new TimeSpan(19, 0, 0))
    };

    /// <summary>Gene: enable Asia session.</summary>
    public bool UseAsia { get; set; } = true;
    /// <summary>Gene: enable London session.</summary>
    public bool UseLondon { get; set; } = true;
    /// <summary>Gene: enable NewYork 1 session.</summary>
    public bool UseNY1 { get; set; } = true;
    /// <summary>Gene: enable NewYork 2 session.</summary>
    public bool UseNY2 { get; set; } = true;

    /// <summary>
    /// Returns 1 if the tick time falls within at least one enabled session, 0 otherwise.
    /// </summary>
    /// <param name="tickTime">The tick timestamp in UTC ticks.</param>
    /// <returns>1 if in session, 0 otherwise.</returns>
    public int IsInSession(long tickTime)
    {
        TimeSpan timeOfDay = new DateTime(tickTime, DateTimeKind.Utc).TimeOfDay;
        bool[] enabled = { UseAsia, UseLondon, UseNY1, UseNY2 };
        for (int i = 0; i < Sessions.Length; i++)
        {
            if (!enabled[i]) continue;
            var (_, start, end) = Sessions[i];
            if (start <= end)
            {
                if (timeOfDay >= start && timeOfDay <= end)
                    return 1;
            }
            else
            {
                if (timeOfDay >= start || timeOfDay <= end)
                    return 1;
            }
        }
        return 0;
    }
}