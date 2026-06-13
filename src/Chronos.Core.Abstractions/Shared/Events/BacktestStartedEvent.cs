namespace Chronos.Core.Abstractions.Shared.Events;

/// <summary>Published when a backtest starts.</summary>
public sealed record BacktestStartedEvent : IMessage
{
    /// <inheritdoc/>
    public required DateTime Timestamp { get; init; }
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
}
