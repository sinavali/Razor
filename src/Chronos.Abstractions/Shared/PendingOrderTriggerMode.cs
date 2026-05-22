namespace Chronos.Abstractions.Shared;

/// <summary>Pending order trigger logic.</summary>
public enum PendingOrderTriggerMode
{
    /// <summary>Use the bid price for buy orders.</summary>
    UseBidForBuy,
    /// <summary>Use the ask price for buy orders.</summary>
    UseAskForBuy,
    /// <summary>Use the mid price for all orders.</summary>
    UseMidPrice
}