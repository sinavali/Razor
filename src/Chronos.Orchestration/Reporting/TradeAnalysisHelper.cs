using Chronos.Core.Trading;
using Chronos.Sdk.Metrics;

namespace Chronos.Orchestration.Reporting;

/// <summary>
/// Shared utilities for trade analysis (per‑symbol metrics, correlation matrix, equity curve).
/// All equity calculations now use actual profit amounts, not margin‑relative returns.
/// </summary>
public static class TradeAnalysisHelper
{
    /// <summary>
    /// Reconstructs an equity curve from a list of closed trades by summing profits.
    /// </summary>
    /// <param name="trades">The trade history.</param>
    /// <param name="initialBalance">The starting account balance.</param>
    /// <returns>A list of equity points in chronological order.</returns>
    public static IReadOnlyList<EquityPoint> ReconstructCurve(IReadOnlyList<Position> trades, double initialBalance)
    {
        ArgumentNullException.ThrowIfNull(trades);
        var curve = new List<EquityPoint>(trades.Count + 1) { new(DateTime.UnixEpoch, initialBalance) };
        double eq = initialBalance;
        foreach (var t in trades)
        {
            eq += t.Profit;
            curve.Add(new EquityPoint(new DateTime(t.CloseTime), eq));
        }
        return curve;
    }

    /// <summary>
    /// Computes per‑symbol performance metrics using actual profits.
    /// </summary>
    public static IReadOnlyDictionary<string, SummaryMetrics> ComputePerSymbolMetrics(
        IReadOnlyList<Position> trades, double initialBalance)
    {
        ArgumentNullException.ThrowIfNull(trades);
        var dict = new Dictionary<string, SummaryMetrics>(StringComparer.OrdinalIgnoreCase);
        var bySymbol = trades.GroupBy(t => t.Symbol);
        foreach (var group in bySymbol)
        {
            var list = group.ToList();
            double netProfit = list.Sum(t => t.Profit);
            double returnPct = initialBalance > 0 ? (netProfit / initialBalance) * 100.0 : 0.0;
            int totalTrades = list.Count;
            double winRate = totalTrades > 0 ? (double)list.Count(t => t.Profit > 0) / totalTrades * 100.0 : 0.0;
            double grossProfit = list.Where(t => t.Profit > 0).Sum(t => t.Profit);
            double grossLoss = Math.Abs(list.Where(t => t.Profit < 0).Sum(t => t.Profit));
            double profitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? double.PositiveInfinity : 0.0;

            // Use account‑level returns for Sharpe/Sortino if AccountEquityAtOpen is available
            var returns = list.Select(t =>
                t.AccountEquityAtOpen > 1e-8 ? t.Profit / t.AccountEquityAtOpen : t.ReturnPct).ToList();

            double sharpe = returns.Count < 2 ? 0.0 : (returns.Average() / Math.Sqrt(returns.Sum(r => Math.Pow(r - returns.Average(), 2)) / (returns.Count - 1)));
            double sortino = 0.0;
            var neg = returns.Where(r => r < 0).ToList();
            if (neg.Count > 0)
            {
                double downVar = neg.Sum(r => r * r) / returns.Count;
                double downDev = Math.Sqrt(downVar);
                sortino = downDev > 0 ? returns.Average() / downDev : 0.0;
            }
            dict[group.Key] = new SummaryMetrics
            {
                NetProfit = netProfit,
                ReturnPct = returnPct,
                TotalTrades = totalTrades,
                WinRatePct = winRate,
                ProfitFactor = profitFactor,
                SharpeRatio = sharpe,
                SortinoRatio = sortino
            };
        }
        return dict;
    }

    /// <summary>
    /// Computes a correlation matrix between symbols based on trade returns.
    /// Uses account‑level returns when possible, falling back to margin‑relative.
    /// </summary>
    public static double[][] ComputeCorrelationMatrix(IReadOnlyList<Position> trades, IReadOnlyList<string> symbols)
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(symbols);
        if (symbols.Count < 2) return [];
        var returnsBySymbol = new Dictionary<string, List<double>>();
        foreach (var sym in symbols)
            returnsBySymbol[sym] = trades.Where(t => t.Symbol == sym)
                .Select(t => t.AccountEquityAtOpen > 1e-8 ? t.Profit / t.AccountEquityAtOpen : t.ReturnPct)
                .ToList();

        int n = symbols.Count;
        double[][] matrix = new double[n][];
        for (int i = 0; i < n; i++)
        {
            matrix[i] = new double[n];
            for (int j = 0; j < n; j++)
            {
                if (i == j) { matrix[i][j] = 1.0; continue; }
                var r1 = returnsBySymbol[symbols[i]];
                var r2 = returnsBySymbol[symbols[j]];
                int len = Math.Min(r1.Count, r2.Count);
                if (len < 2) { matrix[i][j] = 0.0; continue; }
                double avg1 = r1.Take(len).Average();
                double avg2 = r2.Take(len).Average();
                double cov = 0.0, var1 = 0.0, var2 = 0.0;
                for (int k = 0; k < len; k++)
                {
                    double d1 = r1[k] - avg1;
                    double d2 = r2[k] - avg2;
                    cov += d1 * d2;
                    var1 += d1 * d1;
                    var2 += d2 * d2;
                }
                double denom = Math.Sqrt(var1 * var2);
                matrix[i][j] = denom > 0 ? cov / denom : 0.0;
            }
        }
        return matrix;
    }
}
