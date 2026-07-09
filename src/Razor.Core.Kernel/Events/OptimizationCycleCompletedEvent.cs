using Razor.Core.Kernel.Messaging;

namespace Razor.Core.Kernel.Events;

/// <summary>Emitted after a live optimisation cycle completes.</summary>
public sealed record OptimizationCycleCompletedEvent(int CycleIndex, double BestFitness, int GenerationCount) : IMessage
{
    /// <inheritdoc/>
    public required DateTime Timestamp { get; init; }

    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }

    /// <inheritdoc/>
    public string? EventId { get; init; }
}
