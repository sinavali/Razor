using Chronos.Core.Kernel.Messaging;

namespace Chronos.Core.Kernel.Events;

/// <summary>Emitted on live reconnection attempt.</summary>
public sealed record LiveReconnectEvent(bool Success, int AttemptCount, string AdapterName) : IMessage
{
    /// <inheritdoc/>
    public required DateTime Timestamp { get; init; }

    /// <inheritdoc/>
    public Guid? CorrelationId { get; init; }

    /// <inheritdoc/>
    public string? EventId { get; init; }
}
