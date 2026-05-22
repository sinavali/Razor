using Chronos.Core.Trading;
using Chronos.Sdk.Metrics;

namespace Chronos.Orchestration.Reporting;

/// <summary>
/// Assembly of all information needed to generate a report.
/// </summary>
public sealed record ReportData
{
    /// <summary>Key metrics summary.</summary>
    public required SummaryMetrics Summary
    {
        get; init;
    }

    /// <summary>Complete trade list.</summary>
    public required IReadOnlyList<Position> Trades
    {
        get; init;
    }

    /// <summary>Equity points over time for charting.</summary>
    public required IReadOnlyList<EquityPoint> EquityCurve
    {
        get; init;
    }

    /// <summary>Optional population snapshots from optimisation.</summary>
    public IReadOnlyList<GenerationSnapshot>? PopulationHistory
    {
        get; init;
    }

    /// <summary>Per‑symbol performance breakdown (optional).</summary>
    public IReadOnlyDictionary<string, SummaryMetrics>? PerSymbolMetrics
    {
        get; init;
    }

    /// <summary>Correlation matrix between symbols (optional). Rows = symbols.</summary>
    public double[][]? CorrelationMatrix
    {
        get; init;
    }
}

/// <summary>A point on the equity curve.</summary>
public sealed record EquityPoint(DateTime Time, double Equity);

/// <summary>Snapshot of a single generation during optimisation.</summary>
public sealed record GenerationSnapshot(
    int Generation,
    double BestFitness,
    bool IsHyperMutation,
    IReadOnlyList<IndividualSnapshot> Individuals
);

/// <summary>Data for one individual in a generation.</summary>
public sealed record IndividualSnapshot(
    int Index,
    string GeneId,
    double Fitness,
    double[] Genes
);
