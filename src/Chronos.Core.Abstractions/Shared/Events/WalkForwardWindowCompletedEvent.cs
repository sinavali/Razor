namespace Chronos.Core.Abstractions.Shared.Events;

/// <summary>Emitted when a walk‑forward window finishes.</summary>
public sealed record WalkForwardWindowCompletedEvent(int WindowIndex, DateTime TrainStart, DateTime TrainEnd, DateTime TestStart, DateTime TestEnd, double BestFitness) : IMessage
{
    /// <inheritdoc/>
    public required DateTime Timestamp { get; init; }
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
}
