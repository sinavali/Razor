namespace Razor.Core.Sdk.Hooks;

/// <summary>
/// Context provided to report generation hook callbacks.
/// </summary>
public interface IReportContext : IHookContext
{
    /// <summary>The output format of the report (e.g., "PDF", "HTML", "JSON").</summary>
    string ReportFormat { get; }
}
