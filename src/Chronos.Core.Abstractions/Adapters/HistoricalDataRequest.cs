namespace Chronos.Core.Abstractions.Adapters;

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
