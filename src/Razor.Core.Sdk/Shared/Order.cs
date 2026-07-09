namespace Razor.Core.Sdk.Shared;

/// <summary>
/// Represents a pending (untriggered) order.
/// Does not affect equity until triggered and converted into a <see cref="Position"/>.
/// </summary>
public sealed record Order
{
    /// <summary>Unique order ticket.</summary>
    public long Ticket { get; init; }

    /// <summary>Trading symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Order direction and type.</summary>
    public OrderType Type { get; init; }

    /// <summary>Requested volume.</summary>
    public double Volume { get; init; }

    /// <summary>Requested price (for limit/stop orders).</summary>
    public double Price { get; init; }

    /// <summary>Stop‑loss price. 0 = none.</summary>
    public double SL { get; init; }

    /// <summary>Take‑profit price. 0 = none.</summary>
    public double TP { get; init; }

    /// <summary>User comment.</summary>
    public string Comment { get; init; } = string.Empty;
}
