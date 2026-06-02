namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Calculates custom performance metrics.</summary>
public interface IMetricsProvider
{
    /// <summary>Calculates a dictionary of custom metrics.</summary>
    IReadOnlyDictionary<string, double> Calculate(ReportRequest request);
}
