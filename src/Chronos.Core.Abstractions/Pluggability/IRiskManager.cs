namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Portfolio-level risk gate called before every order submission.</summary>
public interface IRiskManager
{
    /// <summary>Evaluates if an order should be placed.</summary>
    bool AllowOrder(RiskContext context, out string? rejectReason);
    /// <summary>Evaluates ongoing portfolio risk.</summary>
    RiskSignal Evaluate(RiskContext context);
}
