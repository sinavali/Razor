namespace Chronos.Abstractions.Adapters;

/// <summary>Response from a fetch operation.</summary>
public sealed record HistoricalDataResponse
{
    /// <summary>Symbol.</summary>
    public string Symbol { get; init; } = string.Empty;
    /// <summary>Indicates whether the operation succeeded.</summary>
    public bool Success { get; init; }
    /// <summary>Error description if failed.</summary>
    public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>Path to the binary file on disk.</summary>
    public string BinaryFilePath { get; init; } = string.Empty;
    /// <summary>Number of tick records fetched.</summary>
    public long TotalRecords { get; init; }
}