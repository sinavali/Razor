using Razor.Core.Kernel.Messaging;

namespace Razor.Core.Kernel.Events;

/// <summary>
/// Message emitted when the adapter connection state changes.
/// </summary>
public sealed record ConnectionStateEvent(bool IsConnected, string AdapterName) : IMessage
{
    /// <inheritdoc/>
    public required DateTime Timestamp { get; init; }

    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }

    /// <inheritdoc/>
    public string? EventId { get; init; }
}
