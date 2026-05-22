using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chronos.Orchestration.HealthChecks;

/// <summary>Reports status of the last live optimisation cycle.</summary>
public sealed class OptimisationStatusHealthCheck : IHealthCheck
{
    private readonly LiveMonitoringState _state;

    /// <summary>Creates a health check using the given <paramref name="state"/>.</summary>
    public OptimisationStatusHealthCheck(LiveMonitoringState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        DateTime lastRun = _state.LastOptimisationRunUtc;
        if (lastRun == default)
            return Task.FromResult(HealthCheckResult.Healthy("No optimisation run yet."));

        var status = _state.LastOptimisationSucceeded ? HealthStatus.Healthy : HealthStatus.Degraded;
        var desc = _state.LastOptimisationSucceeded
            ? "Last optimisation succeeded."
            : "Last optimisation failed.";
        var age = (DateTime.UtcNow - lastRun).TotalMinutes;
        return Task.FromResult(new HealthCheckResult(status, $"{desc} (age: {age:F0} min)"));
    }
}
