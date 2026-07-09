using Razor.Core.Kernel.Backtesting;

namespace Razor.Core.Kernel.Metrics;

/// <summary>
/// Default implementation of <see cref="IMetricsCalculator"/>.
/// Sharpe and Sortino ratios are calculated on a trade‑by‑trade return basis.
/// </summary>
public sealed class MetricsCalculator : IMetricsCalculator
{
    /// <inheritdoc/>
    public SummaryMetrics Calculate(BacktestResult result, double initialBalance,
        DateTime startDate = default, DateTime endDate = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        double netProfit = result.Balance - initialBalance;
        double returnPct = initialBalance > 0 ? (netProfit / initialBalance) * 100.0 : 0.0;

        int totalTrades = result.TotalTrades;
        double winRate = 0.0, grossProfit = 0.0, grossLoss = 0.0;

        var returns = new List<double>(totalTrades);
        foreach (var trade in result.History)
        {
            if (trade.Profit > 0)
            {
                grossProfit += trade.Profit;
            }
            else
            {
                grossLoss += Math.Abs(trade.Profit);
            }

            double tradeReturn = trade.AccountEquityAtOpen > 1e-8
                ? trade.Profit / trade.AccountEquityAtOpen
                : trade.ReturnPct;
            returns.Add(tradeReturn);
        }

        if (totalTrades > 0)
        {
            winRate = (double)result.History.Count(t => t.Profit > 0) / totalTrades * 100.0;
        }

        double profitFactor = grossLoss > 0 ? grossProfit / grossLoss
            : grossProfit > 0 ? double.PositiveInfinity : 0.0;

        double years = 1.0;
        if (startDate != default && endDate != default && endDate > startDate)
        {
            years = (endDate - startDate).TotalDays / 365.25;
        }

        bool annualize = years >= 1.0 / 12.0;

        double sharpe = CalculateSharpeRatio(returns, years, annualize);
        double sortino = CalculateSortinoRatio(returns, years, annualize);
        double calmar = annualize ? (Math.Pow(1 + returnPct / 100.0, 1.0 / years) - 1) * 100.0 / result.Drawdown
            : returnPct / result.Drawdown;
        if (result.Drawdown <= 0)
        {
            calmar = returnPct > 0 ? double.PositiveInfinity : 0.0;
        }

        return new SummaryMetrics
        {
            NetProfit = netProfit,
            ReturnPct = returnPct,
            MaxDrawdownPct = result.Drawdown,
            MaxDailyDrawdownPct = result.DailyDrawdown,
            TotalTrades = totalTrades,
            WinRatePct = winRate,
            ProfitFactor = profitFactor,
            SharpeRatio = sharpe,
            SortinoRatio = sortino,
            CalmarRatio = calmar
        };
    }

    private static double CalculateSharpeRatio(List<double> returns, double years, bool annualize, double riskFreeRate = 0.0)
    {
        if (returns.Count < 2)
        {
            return 0.0;
        }

        double avg = returns.Average();
        double variance = returns.Sum(r => Math.Pow(r - avg, 2)) / (returns.Count - 1);
        double stdDev = Math.Sqrt(variance);
        if (stdDev == 0)
        {
            return 0.0;
        }

        double tradesPerYear = returns.Count / years;
        double sharpe = (avg * tradesPerYear - riskFreeRate) / (stdDev * Math.Sqrt(tradesPerYear));
        return annualize ? sharpe : (avg / stdDev);
    }

    private static double CalculateSortinoRatio(List<double> returns, double years, bool annualize, double riskFreeRate = 0.0)
    {
        if (returns.Count < 2)
        {
            return 0.0;
        }

        double avg = returns.Average();
        var negative = returns.Where(r => r < 0).ToList();
        if (negative.Count == 0)
        {
            return avg > 0 ? double.PositiveInfinity : 0.0;
        }

        double downsideVar = negative.Sum(r => r * r) / returns.Count;
        double downsideDev = Math.Sqrt(downsideVar);
        if (downsideDev == 0)
        {
            return 0.0;
        }

        double tradesPerYear = returns.Count / years;
        double sortino = (avg * tradesPerYear - riskFreeRate) / (downsideDev * Math.Sqrt(tradesPerYear));
        return annualize ? sortino : (avg / downsideDev);
    }
}
