namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Smart order execution algorithm (TWAP, VWAP, Iceberg, etc.).</summary>
public interface IExecutionAlgorithm
{
    /// <summary>Generates a sequence of slices.</summary>
    IAsyncEnumerable<ExecutionSlice> GenerateSlicesAsync(ExecutionAlgorithmRequest request, CancellationToken cancellationToken);
}
