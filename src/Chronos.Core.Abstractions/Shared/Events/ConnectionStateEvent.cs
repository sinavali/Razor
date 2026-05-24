namespace Chronos.Core.Abstractions.Shared.Events;

/// <summary>
/// Message emitted when the adapter connection state changes.
/// </summary>
public sealed record ConnectionStateEvent(bool IsConnected, string AdapterName) : IMessage
{
    /// <inheritdoc/>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }
    /// <inheritdoc/>
    public string? EventId { get; init; }
}
