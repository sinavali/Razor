namespace Razor.Core.Kernel.Clock;

/// <summary>
/// Wall‑clock time for scheduling, health checks, and order guards.
/// Never used for market calculations (Principle 3).
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public long GetTimestamp() => Environment.TickCount64;

    /// <inheritdoc/>
    public DateTime GetUtcNow() => DateTime.UtcNow;
}
