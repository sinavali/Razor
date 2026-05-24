using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Configuration;

/// <summary>
/// Immutable specification for a backtest or optimisation execution.
/// </summary>
public sealed record ExecutionSpecification
{
    /// <summary>Start of the data window (inclusive).</summary>
    public DateTime StartDate { get; init; }

    /// <summary>End of the data window (inclusive).</summary>
    public DateTime EndDate { get; init; }

    /// <summary>Maximum parallel threads (0 = auto).</summary>
    public int MaxParallelThreads { get; init; }

    /// <summary>Simulated execution latency in ticks (0 = instant).</summary>
    public long LatencyTicks { get; init; }

    /// <summary>Number of warm‑up bars before signals are allowed.</summary>
    public int WarmupWindowCount { get; init; }

    /// <summary>Maximum open positions allowed.</summary>
    public int MaxOpenPositions { get; init; }

    /// <summary>Stop‑out level as a ratio (e.g., 0.5 = 50%).</summary>
    public double StopOutLevel { get; init; }

    /// <summary>Policy for cached historical data.</summary>
    public DataActionPolicy HistoricalDataPolicy { get; init; }

    /// <summary>Seed used for deterministic gene initialization when no specific genes are provided.</summary>
    public int GeneInitializationSeed { get; init; }

    /// <summary>Validates this specification.</summary>
    public void Validate()
    {
        if (EndDate <= StartDate) throw new ConfigurationException("EndDate must be after StartDate.");
        if (WarmupWindowCount < 0) throw new ConfigurationException("WarmupWindowCount cannot be negative.");
        if (MaxOpenPositions <= 0) throw new ConfigurationException("MaxOpenPositions must be positive.");
        if (StopOutLevel <= 0 || StopOutLevel > 1) throw new ConfigurationException("StopOutLevel must be between 0 and 1.");
        if (MaxParallelThreads < 0) throw new ConfigurationException("MaxParallelThreads cannot be negative.");
    }

    /// <summary>Creates a validated instance.</summary>
    public static ExecutionSpecification CreateValidated(
        DateTime startDate, DateTime endDate, int warmupBars, int maxOpenPositions, double stopOutLevel,
        long latencyTicks, int geneInitializationSeed,
        int maxParallelThreads = 0,
        DataActionPolicy historicalDataPolicy = DataActionPolicy.DeleteAfterTask)
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
            HistoricalDataPolicy = historicalDataPolicy,
            GeneInitializationSeed = geneInitializationSeed
        };
        spec.Validate();
        return spec;
    }
}
