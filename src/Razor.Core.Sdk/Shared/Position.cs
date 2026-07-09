namespace Razor.Core.Sdk.Shared;

/// <summary>
/// Immutable record of an open or completed position.
/// Created when a pending order is triggered or a market order is executed.
/// </summary>
public sealed record Position
{
    /// <summary>Unique trade identifier.</summary>
    public long Ticket { get; init; }

    /// <summary>Symbol / instrument name.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Direction and order type.</summary>
    public OrderType Type { get; init; }

    /// <summary>Filled volume.</summary>
    public double Volume { get; init; }

    /// <summary>Entry price.</summary>
    public double OpenPrice { get; init; }

    /// <summary>Entry timestamp (ticks).</summary>
    public long OpenTime { get; init; }

    /// <summary>Exit price. 0 if still open.</summary>
    public double ClosePrice { get; init; }

    /// <summary>Exit timestamp. 0 if still open.</summary>
    public long CloseTime { get; init; }

    /// <summary>Stop‑loss price. 0 if none.</summary>
    public double SL { get; init; }

    /// <summary>Take‑profit price. 0 if none.</summary>
    public double TP { get; init; }

    /// <summary>Total commission paid.</summary>
    public double Commission { get; init; }

    /// <summary>Cumulative swap / funding.</summary>
    public double Swap { get; init; }

    /// <summary>Realised or unrealised PnL.</summary>
    public double Profit { get; init; }

    /// <summary>Return on the margin used for this position.</summary>
    public double ReturnPct { get; init; }

    /// <summary>The account equity at the moment the position was opened.</summary>
    public double AccountEquityAtOpen { get; init; }

    /// <summary>Leverage used (0 if spot or unknown).</summary>
    public double Leverage { get; init; }

    /// <summary>Explicit flag for margin trading.</summary>
    public bool IsMargin { get; init; }

    /// <summary>User comment.</summary>
    public string Comment { get; init; } = string.Empty;

    /// <summary>Whether the position is closed.</summary>
    public bool IsClosed => CloseTime > 0;
}
