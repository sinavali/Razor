using Chronos.Abstractions.Adapters;

namespace Chronos.Abstractions.Shared;

/// <summary>
/// Holds borrowed memory‑mapped tick streams together with their file paths,
/// and coordinates cleanup: disposing streams and notifying the adapter.
/// Call <see cref="DisposeAsync"/> after use, or use `await using`.
/// </summary>
public sealed class BorrowedTickData : IAsyncDisposable
{
    private readonly IAdapter _adapter;
    private readonly IReadOnlyList<MemoryMappedTickList> _mappedLists;
    private readonly IReadOnlyList<string> _filePaths;

    /// <summary>The tick streams, one per symbol (aligned with Symbols).</summary>
    public IReadOnlyList<Tick>[] Streams { get; }
    /// <summary>Symbol names in the same order as Streams.</summary>
    public string[] Symbols { get; }

    /// <summary>
    /// Creates a new instance. Called by the data fetch helper.
    /// </summary>
    public BorrowedTickData(
        IReadOnlyList<Tick>[] streams,
        string[] symbols,
        IReadOnlyList<MemoryMappedTickList> mappedLists,
        IReadOnlyList<string> filePaths,
        IAdapter adapter)
    {
        Streams = streams;
        Symbols = symbols;
        _mappedLists = mappedLists;
        _filePaths = filePaths;
        _adapter = adapter;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var mm in _mappedLists)
            mm.Dispose();
        foreach (var path in _filePaths)
            await _adapter.NotifyFileSafeToDeleteAsync(path).ConfigureAwait(false);
    }
}