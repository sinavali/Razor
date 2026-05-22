namespace Chronos.Kernel.Clock;

/// <summary>Abstracts time for trading (tick‑driven) and scheduling (wall‑clock).</summary>
public interface IClock
{
    /// <summary>Current timestamp in 100‑ns ticks.</summary>
    long GetTimestamp();

    /// <summary>Current UTC time.</summary>
    DateTime GetUtcNow();
}