namespace Chronos.Core.Abstractions.Shared;

/// <summary>Response from an execution attempt.</summary>
public sealed record AdapterOrderResponse
{
    /// <summary>Whether the order was accepted.</summary>
    public bool Success { get; init; }
    /// <summary>Error message if rejected.</summary>
    public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Broker‑assigned ticket if successful.</summary>
    public long Ticket { get; init; }
    /// <summary>Execution price.</summary>
    public double ExecutedPrice { get; init; }
    /// <summary>Filled volume.</summary>
    public double ExecutedVolume { get; init; }
}
