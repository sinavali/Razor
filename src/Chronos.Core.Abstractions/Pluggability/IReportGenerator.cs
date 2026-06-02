namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Generates a formatted report.</summary>
public interface IReportGenerator
{
    /// <summary>Format (e.g. HTML, PDF).</summary>
    string Format { get; }
    /// <summary>Generates the report asynchronously.</summary>
    Task<byte[]> GenerateAsync(ReportRequest request, CancellationToken cancellationToken);
}
