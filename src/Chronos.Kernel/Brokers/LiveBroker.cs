#pragma warning disable CA1031 // Reason: General exceptions caught in background loop and event
// handler to prevent process crash per Principle 13.

using System.Collections.Concurrent;
using System.Diagnostics;
using Chronos.Abstractions.Adapters;
using Chronos.Abstractions.Shared;
using Chronos.Abstractions.Shared.Events;
using Chronos.Abstractions.Strategies;
using Chronos.Kernel.Clock;
using Chronos.Kernel.Telemetry;

namespace Chronos.Kernel.Brokers;

/// <summary>
/// Live broker that wraps an <see cref="IAdapter"/> for real exchange trading.
/// Handles state synchronisation, order execution, telemetry, stop‑out protection,
/// and reconnection.  All market‑time calculations are tick‑driven; wall‑clock time is
/// used exclusively for in‑flight guards and telemetry (Principle 3).
/// </summary>
public sealed class LiveBroker : IBroker, IAsyncDisposable
{
    // ── dependencies ──────────────────────────────────────────────
    private readonly IAdapter _adapter;
    private readonly int _magicNumber;
    private readonly IReadOnlyList<INotificationChannel> _notifiers;
    private readonly long _syncIntervalTicks;
    private readonly double _leverage;
    private readonly IMessageBus? _messageBus;
    private readonly TickClock _marketClock;
    private readonly SystemClock _wallClock;
    private readonly int _orderGuardTimeoutSeconds;
    private readonly double _stopOutLevel;

    // ── synchronisation primitives ────────────────────────────────
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private CancellationTokenSource? _lifecycleCts;
    private long _orderSequence;

    // ── mutable state (all access protected by _stateLock) ────────
    private readonly List<Position> _openPositions = [];
    private readonly List<Position> _history = [];
    private readonly List<Order> _pendingOrders = [];
    private readonly Dictionary<long, Position> _positionMap = [];
    private readonly ConcurrentDictionary<string, long> _inFlightOps = new();

    private readonly Dictionary<string, SymbolProperties> _symbolSpecs =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (double Bid, double Ask)> _lastPrices =
        new(StringComparer.OrdinalIgnoreCase);

    private double _balance;
    private double _equity;
    private double _marginUsed;
    private double _peakEquity;
    private double _peakDailyEquity;
    private long _lastSyncTime;
    private long _nextHoldingCostTime;

    // ── IBroker properties ────────────────────────────────────────

    /// <inheritdoc/>
    public double Balance => _balance;

    /// <inheritdoc/>
    public double Equity => _equity;

    /// <inheritdoc/>
    public double MarginUsed => _marginUsed;

    /// <inheritdoc/>
    public double FreeMargin => _equity - _marginUsed;

    /// <inheritdoc/>
    public double MaxDrawdown { get; private set; }

    /// <inheritdoc/>
    public double MaxDailyDrawdown { get; private set; }

    // ── constructor ───────────────────────────────────────────────

    /// <summary>
    /// Creates a new live broker instance.
    /// </summary>
    /// <param name="adapter">The exchange adapter to trade through.</param>
    /// <param name="magicNumber">Magic number for order identification.</param>
    /// <param name="leverage">Account leverage multiplier.</param>
    /// <param name="marketClock">Tick‑driven clock for market operations.</param>
    /// <param name="wallClock">Monotonic clock for scheduling and guards.</param>
    /// <param name="notifiers">Optional notification channels.</param>
    /// <param name="messageBus">Optional message bus for domain events.</param>
    /// <param name="orderGuardTimeoutSeconds">In‑flight order guard timeout in seconds.</param>
    /// <param name="syncInterval">Interval between automatic state synchronisations.</param>
    /// <param name="stopOutLevel">Stop‑out margin level ratio (e.g. 0.5 = 50%).</param>
    public LiveBroker(
        IAdapter adapter,
        int magicNumber,
        double leverage,
        TickClock marketClock,
        SystemClock wallClock,
        IReadOnlyList<INotificationChannel>? notifiers = null,
        IMessageBus? messageBus = null,
        int orderGuardTimeoutSeconds = 5,
        TimeFrame syncInterval = TimeFrame.M1,
        double stopOutLevel = 0.50)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _magicNumber = magicNumber;
        _leverage = leverage;
        _stopOutLevel = stopOutLevel;
        _notifiers = notifiers ?? [];
        _messageBus = messageBus;
        _marketClock = marketClock ?? throw new ArgumentNullException(nameof(marketClock));
        _wallClock = wallClock ?? throw new ArgumentNullException(nameof(wallClock));
        _orderGuardTimeoutSeconds = orderGuardTimeoutSeconds;
        _syncIntervalTicks = (long)syncInterval * TimeSpan.TicksPerMinute;
        _adapter.OnExecutionUpdate += HandleExecutionReport;
    }

    // ── initialisation ────────────────────────────────────────────

    /// <summary>
    /// Stores symbol properties for later use in margin / PnL calculations.
    /// </summary>
    public async Task SetSymbolSpecsAsync(string symbol, SymbolProperties specs)
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _symbolSpecs[symbol] = specs;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task InitializeLiveStateAsync(CancellationToken cancellationToken)
    {
        await ReconcileAsync(cancellationToken).ConfigureAwait(false);

        // Start background periodic sync loop (no async‑void timer).
        _lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => PeriodicSyncLoopAsync(_lifecycleCts.Token), _lifecycleCts.Token);
    }

    /// <summary>
    /// Background loop that calls <see cref="SyncStateAsync"/> every 10 seconds.
    /// </summary>
    private async Task PeriodicSyncLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                await SyncStateAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                /* prevent loop crash – logged via health checks */
            }
        }
    }

    // ── state synchronisation ─────────────────────────────────────

    /// <inheritdoc/>
    public async Task SyncStateAsync(CancellationToken cancellationToken)
    {
        // Prevent overlapping syncs.
        if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var (balance, equity) = await _adapter
                    .GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
                _balance = balance;
                _equity = equity;
                if (_peakEquity == 0) _peakEquity = _peakDailyEquity = equity;

                var active = await _adapter.GetActivePositionsAsync().ConfigureAwait(false);
                _openPositions.Clear();
                _positionMap.Clear();
                foreach (var p in active)
                {
                    _openPositions.Add(p);
                    _positionMap[p.Ticket] = p;
                }

                var pending = await _adapter.GetPendingOrdersAsync().ConfigureAwait(false);
                _pendingOrders.Clear();
                _pendingOrders.AddRange(pending);

                _marginUsed = CalculateMarginUsed();
                UpdateDrawdowns();
                _lastSyncTime = _marketClock.GetTimestamp();
            }
            finally
            {
                _stateLock.Release();
            }
        }
        finally
        {
            _syncGate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (balance, equity) = await _adapter
                .GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
            _balance = balance;
            _equity = equity;
            _peakEquity = _peakDailyEquity = _equity;

            var adapterPositions = await _adapter.GetActivePositionsAsync().ConfigureAwait(false);
            var adapterPosMap = adapterPositions.ToDictionary(p => p.Ticket);

            // Remove local positions that no longer exist on the exchange.
            for (int i = _openPositions.Count - 1; i >= 0; i--)
            {
                if (!adapterPosMap.ContainsKey(_openPositions[i].Ticket))
                {
                    _positionMap.Remove(_openPositions[i].Ticket);
                    _openPositions.RemoveAt(i);
                }
            }

            // Insert new positions; update volume for existing ones.
            foreach (var ap in adapterPositions)
            {
                if (_positionMap.TryGetValue(ap.Ticket, out var local))
                {
                    if (Math.Abs(local.Volume - ap.Volume) > 1e-8)
                    {
                        var updated = local with { Volume = ap.Volume };
                        int idx = _openPositions.IndexOf(local);
                        if (idx >= 0)
                        {
                            _openPositions[idx] = updated;
                            _positionMap[ap.Ticket] = updated;
                        }
                    }
                }
                else
                {
                    _openPositions.Add(ap);
                    _positionMap[ap.Ticket] = ap;
                }
            }

            _pendingOrders.Clear();
            _pendingOrders.AddRange(
                await _adapter.GetPendingOrdersAsync().ConfigureAwait(false));
            _marginUsed = CalculateMarginUsed();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    // ── order entry ───────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> ExecuteMarketOrderAsync(
        string symbol, OrderType type, double volume,
        double sl = 0, double tp = 0, string comment = "")
    {
        string key = NextOrderKey(symbol);
        if (!TryAcquireInFlight(key))
        {
            return new AdapterOrderResponse
            {
                Success = false,
                ErrorMessage = "Duplicate in‑flight order"
            };
        }

        // Margin check using last known price
        if (!_lastPrices.TryGetValue(symbol, out var px))
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Price not available" };
        if (!_symbolSpecs.TryGetValue(symbol, out var spec))
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Symbol properties not available" };

        double refPrice = type == OrderType.Buy ? px.Ask : px.Bid;
        double requiredMargin = _adapter.Calculator.CalculateRequiredMargin(spec, refPrice, volume, _leverage);
        if (FreeMargin < requiredMargin - 1e-8)
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Insufficient margin" };

        Broadcast($"[bold green]LIVE EXECUTION:[/] {type} {volume} {symbol}");
        var sw = Stopwatch.StartNew();
        var response = await _adapter.ExecuteOrderAsync(new AdapterOrderRequest
        {
            Symbol = symbol,
            Type = type,
            Volume = volume,
            StopLoss = sl,
            TakeProfit = tp,
            Comment = comment,
            MagicNumber = _magicNumber
        }).ConfigureAwait(false);
        sw.Stop();
        RecordTelemetry(response.Success, sw.ElapsedMilliseconds);
        if (response.Success)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _marginUsed += requiredMargin;
                UpdateDrawdowns();
            }
            finally
            {
                _stateLock.Release();
            }
        }

        return response;
    }

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> PlacePendingOrderAsync(
        string symbol, OrderType type, double volume,
        double price, double sl, double tp, string comment = "")
    {
        string key = NextOrderKey(symbol);
        if (!TryAcquireInFlight(key))
        {
            return new AdapterOrderResponse
            {
                Success = false,
                ErrorMessage = "Duplicate in‑flight order"
            };
        }

        if (!_symbolSpecs.TryGetValue(symbol, out var spec))
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Symbol properties not available" };

        double requiredMargin = _adapter.Calculator.CalculateRequiredMargin(spec, price, volume, _leverage);
        if (FreeMargin < requiredMargin - 1e-8)
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Insufficient margin" };

        Broadcast($"[bold yellow]LIVE PENDING:[/] {type} {volume} {symbol} @ {price}");
        var sw = Stopwatch.StartNew();
        var response = await _adapter.ExecuteOrderAsync(new AdapterOrderRequest
        {
            Symbol = symbol,
            Type = type,
            Volume = volume,
            Price = price,
            StopLoss = sl,
            TakeProfit = tp,
            Comment = comment,
            MagicNumber = _magicNumber
        }).ConfigureAwait(false);
        sw.Stop();
        RecordTelemetry(response.Success, sw.ElapsedMilliseconds);
        if (response.Success)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _marginUsed += requiredMargin;
                UpdateDrawdowns();
            }
            finally
            {
                _stateLock.Release();
            }
        }

        return response;
    }

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> ModifyOrderAsync(
        long ticket, double? sl = null, double? tp = null, double? price = null)
    {
        var response = await _adapter.ModifyOrderAsync(ticket, sl, tp, price)
            .ConfigureAwait(false);
        if (!response.Success) return response;

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Update local pending order if present.
            for (int i = 0; i < _pendingOrders.Count; i++)
            {
                if (_pendingOrders[i].Ticket == ticket)
                {
                    var o = _pendingOrders[i];
                    if (price.HasValue) o = o with { Price = price.Value };
                    if (sl.HasValue) o = o with { SL = sl.Value };
                    if (tp.HasValue) o = o with { TP = tp.Value };
                    _pendingOrders[i] = o;
                    break;
                }
            }

            // Update local position SL/TP if present.
            if (_positionMap.TryGetValue(ticket, out var pos))
            {
                if (sl.HasValue) pos = pos with { SL = sl.Value };
                if (tp.HasValue) pos = pos with { TP = tp.Value };
                int idx = _openPositions.IndexOf(_positionMap[ticket]);
                if (idx >= 0)
                {
                    _openPositions[idx] = pos;
                    _positionMap[ticket] = pos;
                }
            }
        }
        finally
        {
            _stateLock.Release();
        }

        return response;
    }

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> CancelOrderAsync(long ticket)
    {
        var response = await _adapter.CancelAsync(ticket).ConfigureAwait(false);
        if (!response.Success) return response;

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _pendingOrders.RemoveAll(o => o.Ticket == ticket);
        }
        finally
        {
            _stateLock.Release();
        }

        return response;
    }

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double volume = 0)
    {
        TryAcquireInFlight($"CLOSE_{ticket}");
        var sw = Stopwatch.StartNew();
        var response = await _adapter
            .ClosePositionAsync(ticket, volume > 0 ? volume : null).ConfigureAwait(false);
        sw.Stop();
        RecordTelemetry(response.Success, sw.ElapsedMilliseconds);
        return response;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AdapterOrderResponse>> CloseAllAsync(
        string symbol, OrderType? type = null)
    {
        var tasks = new List<Task<AdapterOrderResponse>>();
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            for (int i = _openPositions.Count - 1; i >= 0; i--)
            {
                if (_openPositions[i].Symbol == symbol
                    && (type == null || _openPositions[i].Type == type))
                {
                    tasks.Add(ClosePositionAsync(_openPositions[i].Ticket));
                }
            }
        }
        finally
        {
            _stateLock.Release();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return [.. tasks.Select(t => t.Result)];
    }

    // ── position queries ──────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> HasOpenPositionAsync(
        string symbol, OrderType? type = null,
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _openPositions.Any(p =>
                p.Symbol == symbol && (type == null || p.Type == type));
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(
        string? symbol = null, CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return symbol == null
                ? [.. _openPositions]
                : _openPositions.Where(p => p.Symbol == symbol).ToList();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Position>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _history.ToList();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Order>> GetPendingOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _pendingOrders.ToList();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    // ── tick processing ───────────────────────────────────────────

    /// <inheritdoc/>
    public async Task OnTickAsync(string symbol, Tick tick)
    {
        _marketClock.SetTickTime(tick.Time);
        long now = _marketClock.GetTimestamp();

        // Record live tick latency for observability.
        long latencyTicks = DateTime.UtcNow.Ticks - now;
        ChronosMetrics.RecordLiveTickLatency(latencyTicks);

        // Trigger periodic sync if interval elapsed.
        if (_syncIntervalTicks > 0 && (now - _lastSyncTime) >= _syncIntervalTicks)
        {
            await SyncStateAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _lastPrices[symbol] = (tick.Bid, tick.Ask);
            ProcessHoldingCosts();

            double floatPl = 0;
            for (int i = _openPositions.Count - 1; i >= 0; i--)
            {
                var p = _openPositions[i];
                (double bid, double ask) = _lastPrices.TryGetValue(p.Symbol, out var px)
                    ? px
                    : (tick.Bid, tick.Ask);

                if (!_symbolSpecs.TryGetValue(p.Symbol, out var spec)) continue;

                double currentPx = p.Type == OrderType.Buy ? bid : ask;
                double rawPnl = _adapter.Calculator.CalculatePnL(
                    spec, p.OpenPrice, currentPx, p.Volume, p.Type);
                var updated = p with { Profit = rawPnl - p.Commission + p.Swap };
                _openPositions[i] = updated;
                _positionMap[p.Ticket] = updated;
                floatPl += updated.Profit;

                // SL / TP check only for the symbol that just received a tick.
                if (p.Symbol == symbol)
                {
                    bool close = false;
                    if (p.Type == OrderType.Buy)
                    {
                        if (p.TP > 0 && bid >= p.TP) close = true;
                        else if (p.SL > 0 && bid <= p.SL) close = true;
                    }
                    else
                    {
                        if (p.TP > 0 && ask <= p.TP) close = true;
                        else if (p.SL > 0 && ask >= p.SL) close = true;
                    }

                    if (close && TryAcquireInFlight($"CLOSE_{p.Ticket}"))
                    {
                        _ = ClosePositionAsync(p.Ticket);
                    }
                }
            }

            _equity = _balance + floatPl;
            UpdateDrawdowns();

            // Stop‑out protection (Principle 13).
            double totalMargin = CalculateMarginUsed();
            if (totalMargin > 0 && _stopOutLevel > 0)
            {
                double marginLevel = _equity / totalMargin;
                if (marginLevel <= _stopOutLevel)
                {
                    ApplyStopOut();
                }
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    // ── connection helpers ────────────────────────────────────────

    /// <summary>
    /// Connects to the adapter and publishes a connection event + updates telemetry.
    /// </summary>
    public async Task ConnectAndNotifyAsync(CancellationToken ct)
    {
        bool connected = await _adapter.ConnectAsync(ct).ConfigureAwait(false);
        if (connected)
        {
            ChronosMetrics.SetConnectionState(true, _adapter.AdapterName);
            _messageBus?.Publish(
                new ConnectionStateEvent(true, _adapter.AdapterName));
        }
    }

    /// <summary>
    /// Disconnects from the adapter and publishes a disconnection event + updates telemetry.
    /// </summary>
    public async Task DisconnectAndNotifyAsync()
    {
        await _adapter.DisconnectAsync().ConfigureAwait(false);
        ChronosMetrics.SetConnectionState(false, _adapter.AdapterName);
        _messageBus?.Publish(
            new ConnectionStateEvent(false, _adapter.AdapterName));
    }

    // ── private helpers ───────────────────────────────────────────

    /// <summary>Calculates total used margin across all open positions.</summary>
    private double CalculateMarginUsed()
    {
        double used = 0;
        foreach (var pos in _openPositions)
        {
            if (_symbolSpecs.TryGetValue(pos.Symbol, out var spec))
            {
                used += _adapter.Calculator.CalculateRequiredMargin(
                    spec, pos.OpenPrice, pos.Volume, _leverage);
            }
        }

        return used;
    }

    /// <summary>
    /// Handles execution reports from the adapter.  Wrapped in try‑catch to prevent
    /// async‑void exceptions from crashing the process (Principle 13).
    /// </summary>
    private async void HandleExecutionReport(ExecutionReport report)
    {
        try
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (report.State is not (ExecutionState.Filled or ExecutionState.PartiallyFilled))
                {
                    return;
                }

                if (_positionMap.TryGetValue(report.Ticket, out var pos))
                {
                    if (report.State == ExecutionState.Filled && report.RemainingVolume <= 0)
                    {
                        // Full close – realise PnL and update balance.
                        double finalProfit = report.RealizedPnL + pos.Swap;
                        _balance += finalProfit;

                        var closedPosition = pos with
                        {
                            ClosePrice = report.ExecutedPrice,
                            CloseTime = report.Timestamp,
                            Profit = finalProfit
                        };
                        _history.Add(closedPosition);
                        _openPositions.Remove(pos);
                        _positionMap.Remove(report.Ticket);
                        Broadcast(
                            $"[bold cyan]POSITION CLOSED:[/] {pos.Symbol} @ {report.ExecutedPrice}");
                        _messageBus?.Publish(new OrderExecutedEvent
                        {
                            Symbol = pos.Symbol,
                            OrderType = pos.Type.ToString(),
                            Volume = report.ExecutedVolume,
                            Price = report.ExecutedPrice,
                            IsOpen = false,
                            CorrelationId = Guid.NewGuid()
                        });
                    }
                    else
                    {
                        // Partial fill – update volume only; do not adjust balance.
                        var updated = pos with
                        {
                            Volume = pos.Volume
                                     + (report.Type == pos.Type
                                         ? report.ExecutedVolume
                                         : -report.ExecutedVolume)
                        };
                        int idx = _openPositions.IndexOf(pos);
                        if (idx >= 0)
                        {
                            _openPositions[idx] = updated;
                            _positionMap[report.Ticket] = updated;
                        }
                    }
                }
                else
                {
                    // New position opened.
                    var newPos = new Position
                    {
                        Ticket = report.Ticket,
                        Symbol = report.Symbol,
                        Type = report.Type,
                        Volume = report.ExecutedVolume,
                        OpenPrice = report.ExecutedPrice,
                        OpenTime = report.Timestamp,
                        Comment = report.Comment,
                        Commission = report.Commission,
                        AccountEquityAtOpen = _equity
                    };
                    _openPositions.Add(newPos);
                    _positionMap[report.Ticket] = newPos;
                    UpdateDrawdowns();
                    _messageBus?.Publish(new OrderExecutedEvent
                    {
                        Symbol = report.Symbol,
                        OrderType = report.Type.ToString(),
                        Volume = report.ExecutedVolume,
                        Price = report.ExecutedPrice,
                        IsOpen = true,
                        CorrelationId = Guid.NewGuid()
                    });
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"LiveBroker.HandleExecutionReport error: {ex}");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Ends the live session gracefully and publishes a session‑ended event with final metrics.
    /// </summary>
    public async Task EndSessionAsync()
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Close all remaining open positions.
            foreach (var pos in _openPositions.ToList())
            {
                _ = ClosePositionAsync(pos.Ticket);
            }

            _messageBus?.Publish(new LiveSessionEndedEvent
            {
                FinalBalance = _balance,
                FinalEquity = _equity,
                MaxDrawdownPct = MaxDrawdown,
                MaxDailyDrawdownPct = MaxDailyDrawdown,
                TotalTrades = _history.Count,
                CorrelationId = Guid.NewGuid()
            });
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>Generates a unique order key for in‑flight tracking.</summary>
    private string NextOrderKey(string symbol)
        => $"{symbol}_ORD_{Interlocked.Increment(ref _orderSequence)}";

    /// <summary>
    /// Returns true if an in‑flight operation is allowed for the given key,
    /// otherwise false (duplicate guard).  Uses monotonic wall‑clock time
    /// (Principle 3).
    /// </summary>
    private bool TryAcquireInFlight(string key)
    {
        long now = _wallClock.GetTimestamp();
        if (_inFlightOps.TryGetValue(key, out long expiry) && now < expiry)
        {
            return false;
        }

        _inFlightOps[key] = now + TimeSpan.FromSeconds(_orderGuardTimeoutSeconds).Ticks;
        return true;
    }

    /// <summary>Sends a message through all registered notification channels.</summary>
    private void Broadcast(string message)
    {
        foreach (var n in _notifiers) _ = n.SendAsync(message);
    }

    /// <summary>Updates peak‑equity drawdown metrics.</summary>
    private void UpdateDrawdowns()
    {
        if (_equity > _peakEquity) _peakEquity = _equity;
        if (_peakEquity > 1e-8)
        {
            double dd = (_peakEquity - _equity) / _peakEquity * 100.0;
            if (dd > MaxDrawdown) MaxDrawdown = dd;
        }

        if (_equity > _peakDailyEquity) _peakDailyEquity = _equity;
        if (_peakDailyEquity > 1e-8)
        {
            double dailyDd = (_peakDailyEquity - _equity) / _peakDailyEquity * 100.0;
            if (dailyDd > MaxDailyDrawdown) MaxDailyDrawdown = dailyDd;
        }
    }

    /// <summary>
    /// Processes daily holding costs (swap/funding) for all open positions.
    /// Daily drawdown peak is reset only upon receiving the first tick of a new UTC
    /// day, because all market‑time calculations are tick‑driven (Principle 3).
    /// </summary>
    private void ProcessHoldingCosts()
    {
        long currentTime = _marketClock.GetTimestamp();
        if (_nextHoldingCostTime == 0)
        {
            _nextHoldingCostTime =
                (currentTime / TimeSpan.TicksPerDay + 1) * TimeSpan.TicksPerDay;
            _peakDailyEquity = _equity;
            return;
        }

        if (currentTime >= _nextHoldingCostTime)
        {
            for (int i = 0; i < _openPositions.Count; i++)
            {
                var pos = _openPositions[i];
                if (_symbolSpecs.TryGetValue(pos.Symbol, out var spec))
                {
                    double cost = _adapter.Calculator.CalculateHoldingCost(
                        spec, pos.Volume, pos.OpenPrice, pos.Type, 0, currentTime);
                    if (Math.Abs(cost) > 0)
                    {
                        var updated = pos with { Swap = pos.Swap + cost };
                        _openPositions[i] = updated;
                        _positionMap[pos.Ticket] = updated;
                    }
                }
            }

            DateTime utcCurrent = new DateTime(currentTime, DateTimeKind.Utc).Date;
            DateTime utcLast =
                new DateTime(currentTime - TimeSpan.TicksPerDay, DateTimeKind.Utc).Date;
            if (utcCurrent != utcLast) _peakDailyEquity = _equity;

            _nextHoldingCostTime =
                (currentTime / TimeSpan.TicksPerDay + 1) * TimeSpan.TicksPerDay;
        }
    }

    /// <summary>
    /// Closes the position with the worst floating PnL when the margin level drops
    /// below the stop‑out threshold.  Optimistically removes the position to avoid
    /// repeated stop‑out attempts.
    /// </summary>
    private void ApplyStopOut()
    {
        if (_openPositions.Count == 0) return;

        int worstIdx = 0;
        double worstProfit = _openPositions[0].Profit;
        for (int i = 1; i < _openPositions.Count; i++)
        {
            if (_openPositions[i].Profit < worstProfit)
            {
                worstProfit = _openPositions[i].Profit;
                worstIdx = i;
            }
        }

        var worstPos = _openPositions[worstIdx];
        if (!TryAcquireInFlight($"CLOSE_{worstPos.Ticket}")) return;

        // Optimistically realise the position.
        double realizedProfit = worstPos.Profit;
        _balance += realizedProfit;
        _openPositions.RemoveAt(worstIdx);
        _positionMap.Remove(worstPos.Ticket);
        _history.Add(worstPos with
        {
            ClosePrice = _lastPrices.TryGetValue(worstPos.Symbol, out var px)
                ? (worstPos.Type == OrderType.Buy ? px.Bid : px.Ask)
                : 0,
            CloseTime = _marketClock.GetTimestamp(),
            Profit = realizedProfit
        });
        _marginUsed = CalculateMarginUsed();

        _ = ClosePositionAsync(worstPos.Ticket);
    }

    /// <summary>Records order outcome telemetry.</summary>
    private static void RecordTelemetry(bool success, long latencyMs)
    {
        if (!success) ChronosMetrics.RecordLiveOrderRejection();
        ChronosMetrics.RecordLiveOrderLatency(latencyMs);
    }

    // ── disposal ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // Gracefully end the session and publish final metrics.
        await EndSessionAsync().ConfigureAwait(false);

        if (_lifecycleCts != null)
        {
            await _lifecycleCts.CancelAsync().ConfigureAwait(false);
            _lifecycleCts.Dispose();
        }

        _adapter.OnExecutionUpdate -= HandleExecutionReport;
        await _adapter.DisconnectAsync().ConfigureAwait(false);
        _stateLock.Dispose();
        _syncGate.Dispose();
    }
}