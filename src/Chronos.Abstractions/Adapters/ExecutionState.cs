namespace Chronos.Abstractions.Adapters;

/// <summary>States an execution report can have.</summary>
public enum ExecutionState
{
    /// <summary>Order has been accepted but not yet filled.</summary>
    New,
    /// <summary>Order has been partially filled.</summary>
    PartiallyFilled,
    /// <summary>Order has been completely filled.</summary>
    Filled,
    /// <summary>Order was cancelled.</summary>
    Canceled,
    /// <summary>Order was rejected.</summary>
    Rejected,
    /// <summary>Order has expired.</summary>
    Expired
}