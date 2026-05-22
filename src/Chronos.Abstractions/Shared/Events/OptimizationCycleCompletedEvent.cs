namespace Chronos.Abstractions.Shared.Events;

/// <summary>Emitted after a live optimisation cycle completes.</summary>
public sealed record OptimizationCycleCompletedEvent(int CycleIndex, double BestFitness, int GenerationCount) : IMessage
{
    /// <inheritdoc/>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
}