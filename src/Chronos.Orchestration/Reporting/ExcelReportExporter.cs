using System.Globalization;
using MiniExcelLibs;

namespace Chronos.Orchestration.Reporting;

/// <summary>
/// Exports a <see cref="ReportData"/> to an Excel workbook using MiniExcel.
/// </summary>
public sealed class ExcelReportExporter : IReportExporter
{
    /// <inheritdoc/>
    public async Task<Stream> ExportAsync(ReportData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var stream = new MemoryStream();

        var summaryRows = new[]
        {
            new { Metric = "Net Profit", Value = data.Summary.NetProfit.ToString("C", CultureInfo.InvariantCulture) },
            new { Metric = "Return %", Value = $"{data.Summary.ReturnPct.ToString("F2", CultureInfo.InvariantCulture)}%" },
            new { Metric = "Max Drawdown %", Value = $"{data.Summary.MaxDrawdownPct.ToString("F2", CultureInfo.InvariantCulture)}%" },
            new { Metric = "Total Trades", Value = data.Summary.TotalTrades.ToString(CultureInfo.InvariantCulture) },
            new { Metric = "Win Rate %", Value = $"{data.Summary.WinRatePct.ToString("F2", CultureInfo.InvariantCulture)}%" },
            new { Metric = "Profit Factor", Value = FormatRatio(data.Summary.ProfitFactor) },
            new { Metric = "Sharpe Ratio", Value = data.Summary.SharpeRatio.ToString("F2", CultureInfo.InvariantCulture) },
            new { Metric = "Sortino Ratio", Value = data.Summary.SortinoRatio.ToString("F2", CultureInfo.InvariantCulture) },
            new { Metric = "Calmar Ratio", Value = FormatRatio(data.Summary.CalmarRatio) }
        };

        var trades = data.Trades.Select(t => new
        {
            t.Ticket,
            t.Symbol,
            Type = t.Type.ToString(),
            t.Volume,
            OpenTime = new DateTime(t.OpenTime).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            t.OpenPrice,
            CloseTime = new DateTime(t.CloseTime).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            t.ClosePrice,
            t.Profit,
            ReturnPct = t.ReturnPct.ToString("P4", CultureInfo.InvariantCulture)
        });

        var equity = data.EquityCurve.Select(e => new
        {
            e.Time,
            e.Equity
        });

        var sheets = new Dictionary<string, object>
        {
            { "Summary", summaryRows },
            { "Trades", trades },
            { "Equity", equity }
        };

        if (data.PopulationHistory is { Count: > 0 })
        {
            var popRows = data.PopulationHistory!.SelectMany(gen =>
                gen.Individuals.Select(ind => new
                {
                    gen.Generation,
                    ind.Index,
                    ind.GeneId,
                    ind.Fitness,
                    Genes = string.Join(",", ind.Genes)
                })
            );
            sheets["Population"] = popRows;
        }

        if (data.PerSymbolMetrics is { Count: > 0 })
        {
            var perSymbolRows = data.PerSymbolMetrics.Select(kvp => new
            {
                Symbol = kvp.Key,
                kvp.Value.NetProfit,
                kvp.Value.ReturnPct,
                kvp.Value.TotalTrades,
                kvp.Value.WinRatePct,
                kvp.Value.ProfitFactor,
                kvp.Value.SharpeRatio,
                kvp.Value.SortinoRatio
            });
            sheets["PerSymbol"] = perSymbolRows;
        }

        if (data.CorrelationMatrix is { Length: > 0 })
        {
            int n = data.CorrelationMatrix.Length;
            var corrRows = new List<Dictionary<string, object>>();
            for (int i = 0; i < n; i++)
            {
                var row = new Dictionary<string, object>();
                for (int j = 0; j < data.CorrelationMatrix[i].Length; j++)
                {
                    row[$"Col{j}"] = data.CorrelationMatrix[i][j].ToString("F4", CultureInfo.InvariantCulture);
                }
                corrRows.Add(row);
            }
            sheets["Correlation"] = corrRows;
        }

        await MiniExcel.SaveAsAsync(stream, sheets, cancellationToken: cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        return stream;
    }

    private static string FormatRatio(double value) =>
        double.IsPositiveInfinity(value) ? "∞" : value.ToString("F2", CultureInfo.InvariantCulture);
}
