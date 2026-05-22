namespace Chronos.Kernel.Clock;

/// <summary>
/// Clock driven by the latest tick timestamp. All trading logic uses this clock.
/// </summary>
public sealed class TickClock : IClock
{
    private long _timestamp;

    /// <summary>Sets the current time from a tick. Must be called at the start of every tick processing.</summary>
    /// <param name="timestamp">The tick’s 100‑ns timestamp.</param>
    public void SetTickTime(long timestamp) => _timestamp = timestamp;

    /// <inheritdoc/>
    public long GetTimestamp() => _timestamp;

    /// <inheritdoc/>
    public DateTime GetUtcNow() => new(_timestamp, DateTimeKind.Utc);
}