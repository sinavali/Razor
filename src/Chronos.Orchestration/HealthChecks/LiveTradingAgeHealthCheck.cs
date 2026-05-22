using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chronos.Orchestration.HealthChecks;

/// <summary>Warns if no tick received recently.</summary>
public sealed class LiveTradingAgeHealthCheck : IHealthCheck
{
    private readonly LiveMonitoringState _state;

    /// <summary>Creates a health check using the given <paramref name="state"/>.</summary>
    public LiveTradingAgeHealthCheck(LiveMonitoringState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        long last = _state.LastTickTimeTicks;
        if (last == 0) return Task.FromResult(HealthCheckResult.Healthy("No ticks received yet."));
        var age = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - last);
        return Task.FromResult(age.TotalSeconds < 30
            ? HealthCheckResult.Healthy($"Last tick {age.TotalSeconds:F0}s ago.")
            : HealthCheckResult.Degraded($"Last tick {age.TotalSeconds:F0}s ago."));
    }
}
