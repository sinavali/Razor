using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chronos.Orchestration.HealthChecks;

/// <summary>
/// Reports whether a background optimisation has run recently.
/// </summary>
public sealed class OptimisationRunningHealthCheck : IHealthCheck
{
    private readonly LiveMonitoringState _state;

    /// <summary>Creates a health check using the given <paramref name="state"/>.</summary>
    public OptimisationRunningHealthCheck(LiveMonitoringState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        DateTime lastOpt = _state.LastOptimisationUtc;
        if (lastOpt == DateTime.MinValue)
            return Task.FromResult(HealthCheckResult.Healthy("Optimisation not yet run."));

        TimeSpan elapsed = DateTime.UtcNow - lastOpt;
        double thresholdHours = _state.ConfiguredIntervalHours > 0
            ? _state.ConfiguredIntervalHours * 1.5
            : 24.0;   // default 24h if not configured

        if (elapsed > TimeSpan.FromHours(thresholdHours))
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"Last optimisation was {elapsed.TotalHours:F1} hours ago."));

        return Task.FromResult(
            HealthCheckResult.Healthy($"Last optimisation was {elapsed.TotalMinutes:F1} minutes ago."));
    }
}
