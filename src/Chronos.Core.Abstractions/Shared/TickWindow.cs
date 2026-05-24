using System.Buffers;

namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Supported price types for querying tick aggregates.
/// </summary>
public enum PriceType
{
    /// <summary>Bid price</summary>
    Bid,
    /// <summary>Ask price</summary>
    Ask,
    /// <summary>Midpoint of bid and ask</summary>
    Mid
}

/// <summary>
/// Sliding‑window helper for indicator and strategy developers.
/// Maintains a ring buffer of recent ticks per symbol and provides
/// on‑demand OHLC statistics without materialising bars.
/// Ticks are the sole source of truth; all calculations use raw tick data.
/// </summary>
public sealed class TickWindow : IDisposable
{
    private readonly Dictionary<string, TickRingBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<TimeFrame, long>> _lastCompleteTimes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxTicksPerSymbol;

    /// <summary>
    /// Stores the last completed bar’s OHLC for immediate retrieval after a window completion event.
    /// </summary>
    private readonly Dictionary<(string Symbol, TimeFrame Timeframe), CompletedBar> _lastCompletedBar = new();

    /// <summary>Raised when a full timeframe window completes (i.e., a new candle would have closed).</summary>
    public event Action<string, TimeFrame>? WindowCompleted;

    /// <summary>Creates a new tick window.</summary>
    /// <param name="symbols">Symbols to track.</param>
    /// <param name="timeframes">Timeframes for which window‑completion events are fired.</param>
    /// <param name="maxTicksPerSymbol">Maximum number of ticks stored per symbol.</param>
    public TickWindow(IEnumerable<string> symbols, IEnumerable<TimeFrame> timeframes, int maxTicksPerSymbol = 100_000)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(timeframes);
        _maxTicksPerSymbol = maxTicksPerSymbol;
        foreach (var sym in symbols)
        {
            _buffers[sym] = new TickRingBuffer(maxTicksPerSymbol);
            var tfDict = new Dictionary<TimeFrame, long>();
            foreach (var tf in timeframes.Where(tf => tf != TimeFrame.Tick))
                tfDict[tf] = 0;
            _lastCompleteTimes[sym] = tfDict;
        }
    }

    /// <summary>Feeds a tick into the window. Fires <see cref="WindowCompleted"/> when a timeframe period ends.</summary>
    public void PushTick(string symbol, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (!_buffers.TryGetValue(symbol, out var buffer)) return;

        if (!_lastCompleteTimes.TryGetValue(symbol, out var times))
        {
            buffer.Add(tick);
            return;
        }

        // Fire completion events BEFORE adding the new tick so the buffer still
        // represents the finished bar.
        foreach (var (tf, lastTime) in times)
        {
            long periodTicks = (long)tf * TimeSpan.TicksPerMinute;
            long currentWindowStart = tick.Time / periodTicks * periodTicks;
            if (currentWindowStart > lastTime)
            {
                // Compute the completed bar’s OHLC while the buffer still holds its ticks
                ComputeAndStoreCompletedBar(symbol, tf, buffer);

                // Notify listeners
                WindowCompleted?.Invoke(symbol, tf);

                times[tf] = currentWindowStart;
            }
        }

        // Now add the new tick (starts populating the next bar)
        buffer.Add(tick);
    }

    /// <summary>
    /// Tries to get the OHLC of the last completed bar for the given symbol/timeframe.
    /// Returns <c>true</c> if such a bar exists and was completed, otherwise <c>false</c>.
    /// </summary>
    public bool TryGetLastCompletedBar(string symbol, TimeFrame tf, out double open, out double high, out double low, out double close, out double volume)
    {
        open = high = low = close = volume = 0;
        if (_lastCompletedBar.TryGetValue((symbol, tf), out var bar))
        {
            if (bar.IsComplete)
            {
                open = bar.Open;
                high = bar.High;
                low = bar.Low;
                close = bar.Close;
                volume = bar.Volume;
                return true;
            }
        }
        return false;
    }

    /// <summary>Returns the most recent ticks for the specified symbol, up to <paramref name="count"/>.</summary>
    public IReadOnlyList<Tick> GetRecentTicks(string symbol, int count)
    {
        if (!_buffers.TryGetValue(symbol, out var buffer)) return Array.Empty<Tick>();
        return buffer.GetMostRecent(count);
    }

    /// <summary>
    /// Retrieves OHLC statistics and indicates whether the window was complete.
    /// (Kept for backward compatibility; prefer <see cref="TryGetLastCompletedBar"/> inside event handlers.)
    /// </summary>
    public void GetCurrentStats(string symbol, TimeFrame tf, PriceType priceType,
        out double open, out double high, out double low, out double close, out double volume, out bool isComplete)
    {
        open = high = low = close = volume = 0;
        isComplete = false;
        if (!_buffers.TryGetValue(symbol, out var buffer)) return;

        long periodTicks = (long)tf * TimeSpan.TicksPerMinute;
        if (periodTicks <= 0) return;
        int n = buffer.Count;
        if (n == 0) return;

        long windowStart = buffer[n - 1].Time / periodTicks * periodTicks;
        int first = n - 1;
        while (first >= 0 && buffer[first].Time >= windowStart) first--;
        first++;
        isComplete = first == 0 || buffer[first - 1].Time < windowStart;

        bool isFirst = true;
        volume = 0;
        for (int i = first; i < n; i++)
        {
            var t = buffer[i];
            if (t.Time < windowStart) continue;
            double price = priceType switch
            {
                PriceType.Ask => t.Ask,
                PriceType.Mid => (t.Bid + t.Ask) * 0.5,
                _ => t.Bid
            };
            volume += t.Volume;
            if (isFirst)
            {
                open = high = low = close = price;
                isFirst = false;
            }
            else
            {
                if (price > high) high = price;
                if (price < low) low = price;
                close = price;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var buf in _buffers.Values) buf.Dispose();
        _buffers.Clear();
    }

    /// <summary>Computes the OHLC of the most recent completed bar and stores it.</summary>
    private void ComputeAndStoreCompletedBar(string symbol, TimeFrame tf, TickRingBuffer buffer)
    {
        long periodTicks = (long)tf * TimeSpan.TicksPerMinute;
        if (periodTicks <= 0) return;
        int n = buffer.Count;
        if (n == 0) return;

        // The last tick in the buffer belongs to the completed bar (since new tick not added yet)
        long lastTime = buffer[n - 1].Time;
        long windowStart = lastTime / periodTicks * periodTicks;

        double open = 0, high = double.MinValue, low = double.MaxValue, close = 0, volume = 0;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var t = buffer[i];
            if (t.Time < windowStart) continue;
            // Use mid‑price for simplicity; easily changeable if needed.
            double price = (t.Bid + t.Ask) * 0.5;
            if (!found)
            {
                open = high = low = close = price;
                found = true;
            }
            else
            {
                if (price > high) high = price;
                if (price < low) low = price;
                close = price;
            }
            volume += t.Volume;
        }

        if (found)
            _lastCompletedBar[(symbol, tf)] = new CompletedBar(open, high, low, close, volume, true);
        else
            _lastCompletedBar[(symbol, tf)] = new CompletedBar(0, 0, 0, 0, 0, false);
    }

    /// <summary>Internal record for completed bar data.</summary>
    private sealed record CompletedBar(double Open, double High, double Low, double Close, double Volume, bool IsComplete);

    private sealed class TickRingBuffer : IDisposable
    {
        private Tick[] _buffer;
        private int _head, _count;
        private readonly int _capacity;
        private bool _disposed;

        public TickRingBuffer(int capacity)
        {
            _capacity = Math.Max(1, capacity);
            _buffer = ArrayPool<Tick>.Shared.Rent(_capacity);
        }

        public int Count => _count;

        public void Add(Tick tick)
        {
            _buffer[_head] = tick;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        public Tick this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
                int start = _count < _capacity ? 0 : _head;
                return _buffer[(start + index) % _capacity];
            }
        }

        public Tick[] GetMostRecent(int count)
        {
            if (_count == 0) return Array.Empty<Tick>();
            int actual = Math.Min(count, _count);
            var result = new Tick[actual];
            int start = _head - actual;
            if (start < 0) start += _capacity;
            for (int i = 0; i < actual; i++) result[i] = _buffer[(start + i) % _capacity];
            return result;
        }

        public void Dispose()
        {
            if (!_disposed && _buffer is not null)
            {
                ArrayPool<Tick>.Shared.Return(_buffer);
                _buffer = [];
                _disposed = true;
                _count = 0;
            }
        }
    }
}
