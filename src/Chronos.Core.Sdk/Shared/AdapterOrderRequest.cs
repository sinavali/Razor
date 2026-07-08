namespace Chronos.Core.Sdk.Shared;

/// <summary>Order request sent to the adapter for execution.</summary>
public sealed record AdapterOrderRequest
{
    /// <summary>Optional magic number to identify the strategy instance.</summary>
    public int? MagicNumber { get; init; }

    /// <summary>Symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Order direction and type.</summary>
    public OrderType Type { get; init; }

    /// <summary>Requested volume.</summary>
    public double Volume { get; init; }

    /// <summary>Price for limit/stop orders.</summary>
    public double Price { get; init; }

    /// <summary>Stop‑loss price. 0 = none.</summary>
    public double StopLoss { get; init; }

    /// <summary>Take‑profit price. 0 = none.</summary>
    public double TakeProfit { get; init; }

    /// <summary>User comment.</summary>
    public string Comment { get; init; } = string.Empty;
}
