namespace Chronos.Core.Abstractions.Adapters;

/// <summary>Handles downloading historical data and streaming it to a binary file.</summary>
public interface IHistoricalDataProvider
{
    /// <summary>Fetches history and writes it to a binary file. Returns the file path and metadata.</summary>
    Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(HistoricalDataRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes a previously cached binary file.</summary>
    Task DeleteHistoryFileAsync(string filePath);

    /// <summary>
    /// Called by Chronos after it has finished reading the binary file. The adapter may now delete it if its retention policy allows.
    /// </summary>
    Task NotifyFileSafeToDeleteAsync(string filePath);
}
