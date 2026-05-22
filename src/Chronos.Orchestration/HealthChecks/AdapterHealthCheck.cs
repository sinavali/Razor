using Chronos.Sdk.Telemetry;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chronos.Orchestration.HealthChecks;

/// <summary>
/// Health check that verifies the adapter is connected.
/// </summary>
public sealed class AdapterHealthCheck : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (ChronosMetrics.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"{ChronosMetrics.AdapterName} is connected."));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("No adapter connected."));
    }
}
