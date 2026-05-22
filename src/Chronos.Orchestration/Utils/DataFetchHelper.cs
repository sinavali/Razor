using Chronos.Core.Adapters;
using Chronos.Core.Data;
using Chronos.Sdk.Data;
using Polly;

namespace Chronos.Orchestration.Utils;

/// <summary>
/// Helpers for downloading historical tick data through an adapter, with retry logic.
/// Returns a <see cref="BorrowedTickData"/> that must be disposed to release memory‑mapped files
/// and notify the adapter.
/// </summary>
public static class DataFetchHelper
{
    /// <summary>
    /// Fetches tick data for all requested symbols and returns streams + symbol names.
    /// Retries up to 3 times with exponential backoff on transient errors.
    /// </summary>
    public static async Task<BorrowedTickData> FetchAllAsync(
        IAdapter adapter,
        IEnumerable<string> symbols,
        DateTime start,
        DateTime end,
        DataActionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(symbols);

        // Materialize to avoid multiple enumeration and CA1851
        var symbolList = symbols.ToList();

        var mappedLists = new List<MemoryMappedTickList>();
        var filePaths = new List<string>();
        var streams = new List<IReadOnlyList<Tick>>();
        var symbolsList = new List<string>();

        var requestedSymbols = new HashSet<string>(symbolList);
        var missingSymbols = new List<string>();

        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        foreach (var sym in symbolList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await retryPolicy.ExecuteAsync(async ct =>
                {
                    var request = new HistoricalDataRequest
                    {
                        Symbol = sym,
                        StartTime = start,
                        EndTime = end,
                        RetentionPolicy = policy
                    };
                    return await adapter.FetchHistoryToBinaryFileAsync(request, ct).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);

                if (response.Success && response.TotalRecords > 0)
                {
                    var mapped = BinaryDataMapper.MapTicks(response.BinaryFilePath);
                    mappedLists.Add(mapped);
                    streams.Add(mapped);
                    symbolsList.Add(sym);
                    filePaths.Add(response.BinaryFilePath);
                }
                else
                {
                    // The adapter reported failure or no records – treat as missing
                    missingSymbols.Add(sym);
                }
            }
#pragma warning disable CA1031
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Retries exhausted; mark symbol as missing
                missingSymbols.Add(sym);
            }
#pragma warning restore CA1031
        }

        // Critical: fail if any requested symbol is missing → no silent data corruption
        if (missingSymbols.Count > 0)
        {
            throw new InvalidOperationException(
                $"Failed to fetch historical data for the following symbols after all retries: {string.Join(", ", missingSymbols)}");
        }

        return new BorrowedTickData(streams.ToArray(), symbolsList.ToArray(), mappedLists, filePaths, adapter);
    }
}
