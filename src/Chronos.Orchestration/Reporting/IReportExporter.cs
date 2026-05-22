namespace Chronos.Orchestration.Reporting;

/// <summary>
/// Exports a <see cref="ReportData"/> to a stream.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Exports the report and returns a stream containing the exported data.
    /// </summary>
    /// <param name="data">The report data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream from which the report can be read.</returns>
    Task<Stream> ExportAsync(ReportData data, CancellationToken cancellationToken = default);
}
