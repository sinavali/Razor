namespace Razor.Core.Sdk.Shared;

/// <summary>Request for historical tick data.</summary>
public sealed record HistoricalDataRequest
{
    /// <summary>Symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Start of data window (UTC).</summary>
    public DateTime StartTime { get; init; }

    /// <summary>End of data window (UTC).</summary>
    public DateTime EndTime { get; init; }

    /// <summary>Retention policy for the generated binary file.</summary>
    public DataActionPolicy RetentionPolicy { get; init; }
}

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
