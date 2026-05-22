using System.Text.Json;

namespace Chronos.Orchestration.Reporting;

/// <summary>
/// Exports <see cref="ReportData"/> as indented JSON.
/// </summary>
public sealed class JsonReportExporter : IReportExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc/>
    public Task<Stream> ExportAsync(ReportData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, data, Options);
        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }
}
