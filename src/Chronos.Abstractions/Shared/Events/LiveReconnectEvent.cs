namespace Chronos.Abstractions.Shared.Events;

/// <summary>Emitted on live reconnection attempt.</summary>
public sealed record LiveReconnectEvent(bool Success, int AttemptCount, string AdapterName) : IMessage
{
    /// <inheritdoc/>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
}