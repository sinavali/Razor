using Chronos.Sdk.Telemetry;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chronos.Orchestration.HealthChecks;

/// <summary>
/// Health check that reports the live engine status based on adapter connectivity.
/// </summary>
public sealed class LiveEngineHealthCheck : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (ChronosMetrics.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Healthy("LiveEngine is connected."));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("LiveEngine is not connected."));
    }
}
