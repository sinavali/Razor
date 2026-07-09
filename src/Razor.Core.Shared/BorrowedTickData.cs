using Razor.Core.Sdk.Shared;
using Razor.Core.Sdk.Slots.Adapter;
using System.Collections.Concurrent;

namespace Razor.Core.Shared;

/// <summary>
/// Holds borrowed memory‑mapped tick streams together with their file paths,
/// and coordinates cleanup: disposing streams and notifying the adapter.
/// Respects <see cref="DataActionPolicy"/> for file deletion.
/// Call <see cref="DisposeAsync"/> after use, or use `await using`.
/// </summary>
public sealed class BorrowedTickData : IAsyncDisposable, IDisposable
{
    // DAT‑05: Reference counting for shared files.
    private static readonly ConcurrentDictionary<string, int> _fileRefCounts = new();
    private static readonly Lock _refCountLock = new();

    private readonly IAdapterCapability _adapter;
    private readonly IReadOnlyList<MemoryMappedTickList> _mappedLists;
    private readonly IReadOnlyList<string> _filePaths;
    private readonly DataActionPolicy _policy;
    private bool _disposed;

    /// <summary>The tick streams, one per symbol (aligned with Symbols).</summary>
    public IReadOnlyList<Tick>[] Streams { get; }

    /// <summary>Symbol names in the same order as Streams.</summary>
    public string[] Symbols { get; }

    /// <summary>
    /// Creates a new instance. Called by the data fetch helper.
    /// </summary>
    /// <param name="streams">Tick streams, one per symbol.</param>
    /// <param name="symbols">Symbol names in the same order as <paramref name="streams"/>.</param>
    /// <param name="mappedLists">Memory‑mapped lists backing the streams.</param>
    /// <param name="filePaths">File paths for each stream.</param>
    /// <param name="adapter">The adapter that provided the data.</param>
    /// <param name="policy">The data retention policy to apply on disposal (default: DeleteAfterTask).</param>
    public BorrowedTickData(
        IReadOnlyList<Tick>[] streams,
        string[] symbols,
        IReadOnlyList<MemoryMappedTickList> mappedLists,
        IReadOnlyList<string> filePaths,
        IAdapterCapability adapter,
        DataActionPolicy policy = DataActionPolicy.DeleteAfterTask)
    {
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(mappedLists);
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(adapter);

        if (streams.Length != symbols.Length)
        {
            throw new ArgumentException($"Streams count ({streams.Length}) must equal symbols count ({symbols.Length}).");
        }

        if (mappedLists.Count != streams.Length)
        {
            throw new ArgumentException($"MappedLists count ({mappedLists.Count}) must equal streams count ({streams.Length}).");
        }

        if (filePaths.Count != streams.Length)
        {
            throw new ArgumentException($"FilePaths count ({filePaths.Count}) must equal streams count ({streams.Length}).");
        }

        Streams = streams;
        Symbols = symbols;
        _mappedLists = mappedLists;
        _filePaths = filePaths;
        _adapter = adapter;
        _policy = policy;

        // DAT‑05: Increment reference count for each file.
        foreach (var path in _filePaths)
        {
            _fileRefCounts.AddOrUpdate(path, 1, (_, count) => count + 1);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var mm in _mappedLists)
        {
            mm.Dispose();
        }

        // DAT‑05: Decrement reference count and delete only when zero.
        foreach (var path in _filePaths)
        {
            bool shouldDelete = false;
            lock (_refCountLock)
            {
                if (_fileRefCounts.TryGetValue(path, out int count))
                {
                    if (count <= 1)
                    {
                        _fileRefCounts.TryRemove(path, out _);
                        shouldDelete = true;
                    }
                    else
                    {
                        _fileRefCounts[path] = count - 1;
                    }
                }
            }

            await _adapter.NotifyFileSafeToDeleteAsync(path).ConfigureAwait(false);

            if (shouldDelete && _policy == DataActionPolicy.DeleteAfterTask)
            {
                await _adapter.DeleteHistoryFileAsync(path).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Synchronous dispose implementation.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
