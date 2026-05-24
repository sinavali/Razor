namespace Chronos.Core.Abstractions.Strategies;

/// <summary>
/// User‑supplied simulation friction model for backtesting.
/// Applies slippage and commission to order fills.
/// </summary>
public interface ISimulationFriction
{
    /// <summary>Calculates slippage for an order fill.</summary>
    double CalculateSlippage(string symbol, Chronos.Core.Abstractions.Shared.OrderType type, double volume, double currentPrice);

    /// <summary>Calculates commission for an order.</summary>
    double CalculateCommission(string symbol, double volume);
}
