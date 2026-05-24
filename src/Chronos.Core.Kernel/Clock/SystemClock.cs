namespace Chronos.Core.Kernel.Clock;

/// <summary>
/// Wall‑clock time for scheduling, health checks, and order guards.
/// Never used for market calculations.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public long GetTimestamp() => Environment.TickCount64;

    /// <inheritdoc/>
    public DateTime GetUtcNow() => DateTime.UtcNow;
}
