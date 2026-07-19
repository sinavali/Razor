// -----------------------------------------------------------------------------
// <copyright file="AuditTrailRecord.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Hooks;

/// <summary>
/// An immutable, append‑only audit trail record describing a single broker order event
/// (executed or rejected) for regulatory compliance.
/// </summary>
internal sealed record AuditTrailRecord
{
    /// <summary>Gets the UTC timestamp of the event (ISO‑8601).</summary>
    public string TimestampUtc { get; init; } = string.Empty;

    /// <summary>Gets the task identifier the order belongs to.</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>Gets the broker‑assigned order identifier.</summary>
    public long OrderId { get; init; }

    /// <summary>Gets the trading symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Gets the order side / type (e.g. Buy, Sell, BuyLimit).</summary>
    public string Side { get; init; } = string.Empty;

    /// <summary>Gets the ordered quantity (volume).</summary>
    public double Quantity { get; init; }

    /// <summary>Gets the order price.</summary>
    public double Price { get; init; }

    /// <summary>Gets the event status (executed or rejected).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets the rejection reason, or empty when the order was executed.</summary>
    public string RejectionReason { get; init; } = string.Empty;
}
