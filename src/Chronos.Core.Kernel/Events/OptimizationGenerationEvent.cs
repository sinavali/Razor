using Chronos.Core.Kernel.Messaging;

namespace Chronos.Core.Kernel.Events;

/// <summary>Published after each generation of an optimization run.</summary>
public sealed record OptimizationGenerationEvent : IMessage
{
    /// <inheritdoc/>
    public required DateTime Timestamp { get; init; }

    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }

    /// <inheritdoc/>
    public string? EventId { get; init; }

    /// <summary>Generation index.</summary>
    public int Generation { get; init; }

    /// <summary>Best fitness in this generation.</summary>
    public double BestFitness { get; init; }

    /// <summary>Whether hyper‑mutation is active.</summary>
    public bool IsHyperMutation { get; init; }
}
