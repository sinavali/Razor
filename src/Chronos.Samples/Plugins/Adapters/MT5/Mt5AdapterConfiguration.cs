using Chronos.Abstractions.Shared;
using System.Diagnostics.CodeAnalysis;

namespace Chronos.Samples.Plugins.Adapters.MT5;

/// <summary>
/// Immutable configuration for an MT5 adapter instance.
/// </summary>
[SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by Mt5Adapter constructor within the same assembly.")]
internal sealed record Mt5AdapterConfiguration
{
    public string BridgeHost { get; init; } = "127.0.0.1";
    public int BridgePort { get; init; } = 5555;
    public int ReconnectIntervalMs { get; init; } = 3000;
    public int MaxReconnectAttempts { get; init; } = 10;
    public int TimeoutMs { get; init; } = 10000;
    public string HistoryCachePath { get; init; } = "Mt5Cache";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BridgeHost))
            throw new ConfigurationException("BridgeHost must not be empty.");
        if (BridgePort <= 0 || BridgePort > 65535)
            throw new ConfigurationException("BridgePort must be between 1 and 65535.");
        if (ReconnectIntervalMs < 100)
            throw new ConfigurationException("ReconnectIntervalMs must be at least 100ms.");
        if (MaxReconnectAttempts < 0)
            throw new ConfigurationException("MaxReconnectAttempts cannot be negative.");
        if (TimeoutMs < 1000)
            throw new ConfigurationException("TimeoutMs must be at least 1000ms.");
        if (string.IsNullOrWhiteSpace(HistoryCachePath))
            throw new ConfigurationException("HistoryCachePath must not be empty.");
    }
}