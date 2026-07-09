using System.Buffers;

namespace Razor.Core.Sdk.Shared;

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
/// on‑demand OHLC statistics without materializing bars.
/// Ticks are the sole source of truth; all calculations use raw tick data.
/// </summary>
/// <remarks>
/// This class is <b>not thread‑safe</b>. It must be used from a single thread,
/// or externally synchronized. The engine guarantees that all tick processing
/// (backtest loop, live tick handler) runs on a single thread, so no additional
/// locking is required when used inside a strategy's <c>OnTick</c> method.
/// </remarks>
public sealed class TickWindow : IDisposable
{
    private readonly Dictionary<string, TickRingBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Dictionary<TimeFrame, long>> _lastCompleteTimes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly int _maxTicksPerSymbol;

    private readonly Dictionary<(string Symbol, TimeFrame Timeframe), CompletedBar> _lastCompletedBar = new();

    // Rolling accumulators for O(1) GetCurrentStats
    private readonly Dictionary<(string Symbol, TimeFrame Timeframe), RollingStats> _rollingStats = new();

    /// <summary>
    /// Raised when a full timeframe window completes (i.e., a new candle would have closed).
    /// </summary>
    public event Action<string, TimeFrame>? WindowCompleted;

    /// <summary>Creates a new tick window.</summary>
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
            {
                tfDict[tf] = 0;
                _rollingStats[(sym, tf)] = new RollingStats();
            }

            _lastCompleteTimes[sym] = tfDict;
        }
    }

    /// <summary>Feeds a tick into the window. Fires <see cref="WindowCompleted"/> when a timeframe period ends.</summary>
    public void PushTick(string symbol, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (!_buffers.TryGetValue(symbol, out var buffer))
        {
            return;
        }

        if (!_lastCompleteTimes.TryGetValue(symbol, out var times))
        {
            buffer.Add(tick);
            return;
        }

        foreach (var (tf, lastTime) in times)
        {
            long periodTicks = (long)tf * TimeSpan.TicksPerMinute;
            long currentWindowStart = tick.Time / periodTicks * periodTicks;
            if (currentWindowStart > lastTime)
            {
                // Finalise the completed bar using the rolling accumulator
                if (_rollingStats.TryGetValue((symbol, tf), out var stats) && stats.Count > 0)
                {
                    _lastCompletedBar[(symbol, tf)] = new CompletedBar(
                        stats.Open, stats.High, stats.Low, stats.Close, stats.Volume, true);
                }
                else
                {
                    // Fallback: compute from buffer
                    ComputeAndStoreCompletedBar(symbol, tf, buffer);
                }

                // Reset rolling stats for the new window
                _rollingStats[(symbol, tf)] = new RollingStats();
                WindowCompleted?.Invoke(symbol, tf);
                times[tf] = currentWindowStart;
            }

            // Update rolling stats with this tick
            if (_rollingStats.TryGetValue((symbol, tf), out var rolling))
            {
                double price = (tick.Bid + tick.Ask) * 0.5;
                if (rolling.Count == 0)
                {
                    rolling.Open = price;
                }

                rolling.High = Math.Max(rolling.High, price);
                rolling.Low = rolling.Count == 0 ? price : Math.Min(rolling.Low, price);
                rolling.Close = price;
                rolling.Volume += tick.Volume;
                rolling.Count++;
            }
        }

        buffer.Add(tick);
    }

    /// <summary>
    /// Tries to get the OHLC of the last completed bar for the given symbol/timeframe.
    /// Returns <c>true</c> if such a bar exists and was completed, otherwise <c>false</c>.
    /// </summary>
    public bool TryGetLastCompletedBar(string symbol, TimeFrame tf, out double open, out double high, out double low,
        out double close, out double volume)
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
        if (!_buffers.TryGetValue(symbol, out var buffer))
        {
            return Array.Empty<Tick>();
        }

        return buffer.GetMostRecent(count);
    }

    /// <summary>
    /// Zero‑allocation copy of recent ticks into the given span.
    /// Returns the number of ticks actually written.
    /// </summary>
    public int CopyRecentTicks(string symbol, Span<Tick> destination, int maxCount)
    {
        if (!_buffers.TryGetValue(symbol, out var buffer))
        {
            return 0;
        }

        return buffer.CopyMostRecent(destination, maxCount);
    }

    /// <summary>
    /// Retrieves OHLC statistics using rolling accumulators (O(1)) and indicates whether the window was complete.
    /// </summary>
    public void GetCurrentStats(string symbol, TimeFrame tf, PriceType priceType,
        out double open, out double high, out double low, out double close, out double volume, out bool isComplete)
    {
        open = high = low = close = volume = 0;
        isComplete = false;
        if (!_buffers.TryGetValue(symbol, out var buffer))
        {
            return;
        }

        long periodTicks = (long)tf * TimeSpan.TicksPerMinute;
        if (periodTicks <= 0)
        {
            return;
        }

        int n = buffer.Count;
        if (n == 0)
        {
            return;
        }

        long windowStart = buffer[n - 1].Time / periodTicks * periodTicks;
        int first = n - 1;
        while (first >= 0 && buffer[first].Time >= windowStart)
        {
            first--;
        }

        first++;
        isComplete = first == 0 || buffer[first - 1].Time < windowStart;

        // Use rolling stats if available and up‑to‑date
        if (_rollingStats.TryGetValue((symbol, tf), out var stats) && stats.Count > 0)
        {
            open = stats.Open;
            high = stats.High;
            low = stats.Low;
            close = stats.Close;
            volume = stats.Volume;
            // isComplete already computed
            return;
        }

        // Fallback: compute from buffer (should rarely happen)
        bool isFirst = true;
        volume = 0;
        for (int i = first; i < n; i++)
        {
            var t = buffer[i];
            if (t.Time < windowStart)
            {
                continue;
            }

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
                if (price > high)
                {
                    high = price;
                }

                if (price < low)
                {
                    low = price;
                }

                close = price;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var buf in _buffers.Values)
        {
            buf.Dispose();
        }

        _buffers.Clear();
        _rollingStats.Clear();
    }

    private void ComputeAndStoreCompletedBar(string symbol, TimeFrame tf, TickRingBuffer buffer)
    {
        long periodTicks = (long)tf * TimeSpan.TicksPerMinute;
        if (periodTicks <= 0)
        {
            return;
        }

        int n = buffer.Count;
        if (n == 0)
        {
            return;
        }

        long lastTime = buffer[n - 1].Time;
        long windowStart = lastTime / periodTicks * periodTicks;

        double open = 0, high = double.MinValue, low = double.MaxValue, close = 0, volume = 0;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var t = buffer[i];
            if (t.Time < windowStart)
            {
                continue;
            }

            double price = (t.Bid + t.Ask) * 0.5;
            if (!found)
            {
                open = high = low = close = price;
                found = true;
            }
            else
            {
                if (price > high)
                {
                    high = price;
                }

                if (price < low)
                {
                    low = price;
                }

                close = price;
            }

            volume += t.Volume;
        }

        if (found)
        {
            _lastCompletedBar[(symbol, tf)] = new CompletedBar(open, high, low, close, volume, true);
        }
        else
        {
            _lastCompletedBar[(symbol, tf)] = new CompletedBar(0, 0, 0, 0, 0, false);
        }
    }

    private sealed record CompletedBar(
        double Open,
        double High,
        double Low,
        double Close,
        double Volume,
        bool IsComplete);

    private sealed class RollingStats
    {
        public double Open;
        public double High;
        public double Low;
        public double Close;
        public double Volume;
        public int Count;
    }

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
            if (_count < _capacity)
            {
                _count++;
            }
        }

        public Tick this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                // DAT‑02: Fix the start calculation for correct retrieval.
                int start;
                if (_count == _capacity)
                {
                    // Buffer is full: the oldest valid tick is at _head.
                    start = _head;
                }
                else
                {
                    // Buffer is not full: the oldest valid tick is at index 0.
                    start = 0;
                }

                return _buffer[(start + index) % _capacity];
            }
        }

        public int CopyMostRecent(Span<Tick> destination, int maxCount)
        {
            if (_count == 0 || maxCount <= 0 || destination.Length == 0)
            {
                return 0;
            }

            int actual = Math.Min(Math.Min(maxCount, _count), destination.Length);
            int start;
            if (_count == _capacity)
            {
                // Buffer is full: the oldest valid tick is at _head.
                start = _head;
            }
            else
            {
                // Buffer is not full: the oldest valid tick is at index 0.
                start = 0;
            }

            // We need to get the 'actual' most recent ticks.
            // The most recent tick is at index (_count - 1) relative to start.
            int recentStart = (start + _count - actual) % _capacity;
            for (int i = 0; i < actual; i++)
            {
                destination[i] = _buffer[(recentStart + i) % _capacity];
            }

            return actual;
        }

        public Tick[] GetMostRecent(int count)
        {
            if (_count == 0)
            {
                return Array.Empty<Tick>();
            }

            int actual = Math.Min(count, _count);
            var result = new Tick[actual];
            int start;
            if (_count == _capacity)
            {
                // Buffer is full: the oldest valid tick is at _head.
                start = _head;
            }
            else
            {
                // Buffer is not full: the oldest valid tick is at index 0.
                start = 0;
            }

            int recentStart = (start + _count - actual) % _capacity;
            for (int i = 0; i < actual; i++)
            {
                result[i] = _buffer[(recentStart + i) % _capacity];
            }

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
