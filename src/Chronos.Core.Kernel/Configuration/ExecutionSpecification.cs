using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Configuration;

/// <summary>
/// Immutable specification for a backtest or optimisation execution.
/// </summary>
public sealed record ExecutionSpecification
{
    /// <summary>Start of the data window (inclusive).</summary>
    public required DateTime StartDate { get; init; }

    /// <summary>End of the data window (inclusive).</summary>
    public required DateTime EndDate { get; init; }

    /// <summary>Maximum parallel threads (0 = auto).</summary>
    public int MaxParallelThreads { get; init; }

    /// <summary>Simulated execution latency in ticks (0 = instant).</summary>
    public long LatencyTicks { get; init; }

    /// <summary>Number of warm‑up bars before signals are allowed.</summary>
    public int WarmupWindowCount { get; init; }

    /// <summary>Maximum open positions allowed.</summary>
    public required int MaxOpenPositions { get; init; }

    /// <summary>Stop‑out level as a ratio (e.g., 0.5 = 50%).</summary>
    public required double StopOutLevel { get; init; }

    /// <summary>
    /// Seed used for deterministic gene initialization when no explicit genes are provided.
    /// A value of <c>null</c> indicates that the strategy's default gene values should be used.
    /// </summary>
    public int? GeneInitializationSeed { get; init; }

    /// <summary>Validates this specification.</summary>
    public void Validate()
    {
        if (EndDate <= StartDate)
        {
            throw new ConfigurationException("EndDate must be after StartDate.");
        }

        if (WarmupWindowCount < 0)
        {
            throw new ConfigurationException("WarmupWindowCount cannot be negative.");
        }

        if (MaxOpenPositions <= 0)
        {
            throw new ConfigurationException("MaxOpenPositions must be positive.");
        }

        if (StopOutLevel <= 0 || StopOutLevel > 1)
        {
            throw new ConfigurationException("StopOutLevel must be between 0 and 1.");
        }

        if (MaxParallelThreads < 0)
        {
            throw new ConfigurationException("MaxParallelThreads cannot be negative.");
        }

        if (GeneInitializationSeed.HasValue && GeneInitializationSeed.Value < 0)
        {
            throw new ConfigurationException("GeneInitializationSeed must be non‑negative when provided.");
        }
    }

    /// <summary>Creates a validated instance.</summary>
    public static ExecutionSpecification CreateValidated(
        DateTime startDate,
        DateTime endDate,
        int warmupBars,
        int maxOpenPositions,
        double stopOutLevel,
        long latencyTicks = 0,
        int? geneInitializationSeed = null,
        int maxParallelThreads = 0)
    {
        var spec = new ExecutionSpecification
        {
            StartDate = startDate,
            EndDate = endDate,
            WarmupWindowCount = warmupBars,
            MaxOpenPositions = maxOpenPositions,
            StopOutLevel = stopOutLevel,
            MaxParallelThreads = maxParallelThreads,
            LatencyTicks = latencyTicks,
            GeneInitializationSeed = geneInitializationSeed
        };
        spec.Validate();
        return spec;
    }
}
