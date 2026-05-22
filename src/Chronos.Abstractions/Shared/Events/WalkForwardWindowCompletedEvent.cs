namespace Chronos.Abstractions.Shared.Events;

/// <summary>Emitted when a walk‑forward window finishes.</summary>
public sealed record WalkForwardWindowCompletedEvent(int WindowIndex, DateTime TrainStart, DateTime TrainEnd, DateTime TestStart, DateTime TestEnd, double BestFitness) : IMessage
{
    /// <inheritdoc/>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
}