#pragma warning disable CA1031 // Reason: General exceptions caught in background loop and event
// handler to prevent process crash per Principle 13.

using Chronos.Core.Abstractions.Adapters;
using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Shared.Events;
using Chronos.Core.Abstractions.Strategies;
using Chronos.Core.Abstractions.Telemetry;
using Chronos.Core.Kernel.Clock;
using Chronos.Core.Kernel.Messaging;
using Chronos.Core.Kernel.Telemetry;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Chronos.Core.Kernel.Brokers;

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
    private readonly IChronosMetrics _metrics;
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
    /// <inheritdoc/>
    public bool IsWarmup => false; // Live instances are never in warmup logic mode

    // ── constructor ───────────────────────────────────────────────

    /// <summary>Constructs the live broker.</summary>
    public LiveBroker(
        IAdapter adapter,
        int magicNumber,
        double leverage,
        TickClock marketClock,
        SystemClock wallClock,
        IChronosMetrics metrics,
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
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _notifiers = notifiers ?? [];
        _messageBus = messageBus;
        _marketClock = marketClock ?? throw new ArgumentNullException(nameof(marketClock));
        _wallClock = wallClock ?? throw new ArgumentNullException(nameof(wallClock));
        _orderGuardTimeoutSeconds = orderGuardTimeoutSeconds;
        _syncIntervalTicks = (long)syncInterval * TimeSpan.TicksPerMinute;

        _adapter.OnExecutionUpdate += report =>
            _ = Task.Run(() => HandleExecutionReportAsync(report)).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Trace.TraceError($"LiveBroker.HandleExecutionReport error: {t.Exception}");
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    // ── initialisation ────────────────────────────────────────────

    /// <summary>Applies specific Symbol Properties internally.</summary>
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
        _lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => PeriodicSyncLoopAsync(_lifecycleCts.Token), _lifecycleCts.Token);
    }

    private async Task PeriodicSyncLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                await SyncStateAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch { /* prevent loop crash – logged via health checks */ }
        }
    }

    // ── state synchronisation ─────────────────────────────────────

    /// <inheritdoc/>
    public async Task SyncStateAsync(CancellationToken cancellationToken)
    {
        if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return;

        try
        {
            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var (balance, equity) = await _adapter.GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
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
            finally { _stateLock.Release(); }
        }
        finally { _syncGate.Release(); }
    }

    /// <summary>Forces a complete reconciliation against the adapter states.</summary>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (balance, equity) = await _adapter.GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
            _balance = balance;
            _equity = equity;
            _peakEquity = _peakDailyEquity = _equity;

            var adapterPositions = await _adapter.GetActivePositionsAsync().ConfigureAwait(false);
            var adapterPosMap = adapterPositions.ToDictionary(p => p.Ticket);

            for (int i = _openPositions.Count - 1; i >= 0; i--)
            {
                if (!adapterPosMap.ContainsKey(_openPositions[i].Ticket))
                {
                    _positionMap.Remove(_openPositions[i].Ticket);
                    _openPositions.RemoveAt(i);
                }
            }

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
            _pendingOrders.AddRange(await _adapter.GetPendingOrdersAsync().ConfigureAwait(false));
            _marginUsed = CalculateMarginUsed();
        }
        finally { _stateLock.Release(); }
    }

    // ── order entry ───────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> ExecuteMarketOrderAsync(
        string symbol, OrderType type, double volume, double sl = 0, double tp = 0, string comment = "")
    {
        string key = NextOrderKey(symbol);
        if (!TryAcquireInFlight(key)) return new AdapterOrderResponse { Success = false, ErrorMessage = "Duplicate in‑flight order" };

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
            finally { _stateLock.Release(); }
        }
        return response;
    }

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> PlacePendingOrderAsync(
        string symbol, OrderType type, double volume, double price, double sl, double tp, string comment = "")
    {
        string key = NextOrderKey(symbol);
        if (!TryAcquireInFlight(key)) return new AdapterOrderResponse { Success = false, ErrorMessage = "Duplicate in‑flight order" };

        if (!_symbolSpecs.TryGetValue(symbol, out var spec))
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Symbol properties not available" };

        double requiredMargin = _adapter.Calculator.CalculateRequiredMargin(spec, price, volume, _leverage);
        if (FreeMargin < requiredMargin - 1e-8) return new AdapterOrderResponse { Success = false, ErrorMessage = "Insufficient margin" };

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
            finally { _stateLock.Release(); }
        }
        return response;
    }

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null)
    {
        var response = await _adapter.ModifyOrderAsync(ticket, sl, tp, price).ConfigureAwait(false);
        if (!response.Success) return response;

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
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
        finally { _stateLock.Release(); }
        return response;
    }

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> CancelOrderAsync(long ticket)
    {
        var response = await _adapter.CancelAsync(ticket).ConfigureAwait(false);
        if (!response.Success) return response;
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try { _pendingOrders.RemoveAll(o => o.Ticket == ticket); }
        finally { _stateLock.Release(); }
        return response;
    }

    /// <inheritdoc/>
    public async Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double volume = 0)
    {
        TryAcquireInFlight($"CLOSE_{ticket}");
        var sw = Stopwatch.StartNew();
        var response = await _adapter.ClosePositionAsync(ticket, volume > 0 ? volume : null).ConfigureAwait(false);
        sw.Stop();
        RecordTelemetry(response.Success, sw.ElapsedMilliseconds);
        return response;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AdapterOrderResponse>> CloseAllAsync(string symbol, OrderType? type = null)
    {
        var tasks = new List<Task<AdapterOrderResponse>>();
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            for (int i = _openPositions.Count - 1; i >= 0; i--)
            {
                if (_openPositions[i].Symbol == symbol && (type == null || _openPositions[i].Type == type))
                {
                    tasks.Add(ClosePositionAsync(_openPositions[i].Ticket));
                }
            }
        }
        finally { _stateLock.Release(); }
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return [.. tasks.Select(t => t.Result)];
    }

    // ── position queries ──────────────────────────────────────────
    /// <inheritdoc/>
    public async Task<bool> HasOpenPositionAsync(string symbol, OrderType? type = null, CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return _openPositions.Any(p => p.Symbol == symbol && (type == null || p.Type == type)); }
        finally { _stateLock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return symbol == null ? [.. _openPositions] : _openPositions.Where(p => p.Symbol == symbol).ToList(); }
        finally { _stateLock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Position>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return _history.ToList(); }
        finally { _stateLock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return _pendingOrders.ToList(); }
        finally { _stateLock.Release(); }
    }

    // ── tick processing ───────────────────────────────────────────

    /// <summary>Asynchronously routes incoming ticks.</summary>
    public async Task OnTickAsync(string symbol, Tick tick)
    {
        _marketClock.SetTickTime(tick.Time);
        long now = _marketClock.GetTimestamp();

        long latencyTicks = DateTime.UtcNow.Ticks - now;
        _metrics.RecordLiveTickLatency(latencyTicks);

        if (_syncIntervalTicks > 0 && (now - _lastSyncTime) >= _syncIntervalTicks)
            await SyncStateAsync(CancellationToken.None).ConfigureAwait(false);

        List<long>? ticketsToClose = null;
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _lastPrices[symbol] = (tick.Bid, tick.Ask);
            ProcessHoldingCosts();

            double floatPl = 0;
            for (int i = _openPositions.Count - 1; i >= 0; i--)
            {
                var p = _openPositions[i];
                (double bid, double ask) = _lastPrices.TryGetValue(p.Symbol, out var px) ? px : (tick.Bid, tick.Ask);
                if (!_symbolSpecs.TryGetValue(p.Symbol, out var spec)) continue;

                double currentPx = p.Type == OrderType.Buy ? bid : ask;
                double rawPnl = _adapter.Calculator.CalculatePnL(spec, p.OpenPrice, currentPx, p.Volume, p.Type);
                var updated = p with { Profit = rawPnl - p.Commission + p.Swap };
                _openPositions[i] = updated;
                _positionMap[p.Ticket] = updated;
                floatPl += updated.Profit;

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

                    if (close)
                    {
                        ticketsToClose ??= new List<long>();
                        ticketsToClose.Add(p.Ticket);
                    }
                }
            }

            _equity = _balance + floatPl;
            UpdateDrawdowns();

            double totalMargin = CalculateMarginUsed();
            if (totalMargin > 0 && _stopOutLevel > 0)
            {
                double marginLevel = _equity / totalMargin;
                if (marginLevel <= _stopOutLevel) ApplyStopOut();
            }
        }
        finally { _stateLock.Release(); }

        if (ticketsToClose != null)
        {
            foreach (var ticket in ticketsToClose)
            {
                try
                {
                    await ClosePositionAsync(ticket).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Close failed for ticket {ticket}: {ex}");
                }
            }
        }
    }

    /// <summary>Connects safely handling metrics.</summary>
    public async Task ConnectAndNotifyAsync(CancellationToken ct)
    {
        bool connected = await _adapter.ConnectAsync(ct).ConfigureAwait(false);
        if (connected)
        {
            _metrics.SetConnectionState(true, _adapter.AdapterName);
            _messageBus?.Publish(new ConnectionStateEvent(true, _adapter.AdapterName));
        }
    }

    /// <summary>Disconnects safely handling metrics.</summary>
    public async Task DisconnectAndNotifyAsync()
    {
        await _adapter.DisconnectAsync().ConfigureAwait(false);
        _metrics.SetConnectionState(false, _adapter.AdapterName);
        _messageBus?.Publish(new ConnectionStateEvent(false, _adapter.AdapterName));
    }

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

    private async Task HandleExecutionReportAsync(ExecutionReport report)
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (report.State is not (ExecutionState.Filled or ExecutionState.PartiallyFilled)) return;

            if (_positionMap.TryGetValue(report.Ticket, out var pos))
            {
                if (report.State == ExecutionState.Filled && report.RemainingVolume <= 0)
                {
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
                    Broadcast($"[bold cyan]POSITION CLOSED:[/] {pos.Symbol} @ {report.ExecutedPrice}");
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
                    var updated = pos with
                    {
                        Volume = pos.Volume + (report.Type == pos.Type ? report.ExecutedVolume : -report.ExecutedVolume)
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
        finally { _stateLock.Release(); }
    }

    /// <summary>Terminates the live session cleanly, waiting for closure fills.</summary>
    public async Task EndSessionAsync()
    {
        List<Position> positionsToClose;
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            positionsToClose = _openPositions.ToList();
            // ARCH-11 Fix: DO NOT clear the active positions mapping here. 
            // Allow the execution reports resolving to close them gracefully, avoiding duplicates.
        }
        finally { _stateLock.Release(); }

        var closeTasks = positionsToClose.Select(p => ClosePositionAsync(p.Ticket));
        await Task.WhenAll(closeTasks).ConfigureAwait(false);

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _openPositions.Clear();
            _positionMap.Clear();
        }
        finally { _stateLock.Release(); }

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

    private string NextOrderKey(string symbol) => $"{symbol}_ORD_{Interlocked.Increment(ref _orderSequence)}";

    private bool TryAcquireInFlight(string key)
    {
        // BUG-02 Fix: WallClock is TickCount64 (milliseconds). Use milliseconds duration.
        long now = _wallClock.GetTimestamp();
        if (_inFlightOps.TryGetValue(key, out long expiry) && now < expiry) return false;

        _inFlightOps[key] = now + (long)_orderGuardTimeoutSeconds * 1000L;
        return true;
    }

    private void Broadcast(string message) { foreach (var n in _notifiers) _ = n.SendAsync(message); }

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

    private void ProcessHoldingCosts()
    {
        long currentTime = _marketClock.GetTimestamp();
        if (_nextHoldingCostTime == 0)
        {
            _nextHoldingCostTime = (currentTime / TimeSpan.TicksPerDay + 1) * TimeSpan.TicksPerDay;
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
                    // BUG-01 Fix: Start of charge period correctly anchored 
                    long prevBoundary = _nextHoldingCostTime - TimeSpan.TicksPerDay;
                    double cost = _adapter.Calculator.CalculateHoldingCost(
                        spec, pos.Volume, pos.OpenPrice, pos.Type, prevBoundary, currentTime);

                    if (Math.Abs(cost) > 0)
                    {
                        var updated = pos with { Swap = pos.Swap + cost };
                        _openPositions[i] = updated;
                        _positionMap[pos.Ticket] = updated;
                    }
                }
            }

            DateTime utcCurrent = new DateTime(currentTime, DateTimeKind.Utc).Date;
            DateTime utcLast = new DateTime(currentTime - TimeSpan.TicksPerDay, DateTimeKind.Utc).Date;
            if (utcCurrent != utcLast) _peakDailyEquity = _equity;

            _nextHoldingCostTime = (currentTime / TimeSpan.TicksPerDay + 1) * TimeSpan.TicksPerDay;
        }
    }

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

        double realizedProfit = worstPos.Profit;
        _balance += realizedProfit;
        _openPositions.RemoveAt(worstIdx);
        _positionMap.Remove(worstPos.Ticket);
        _history.Add(worstPos with
        {
            ClosePrice = _lastPrices.TryGetValue(worstPos.Symbol, out var px)
                ? (worstPos.Type == OrderType.Buy ? px.Bid : px.Ask) : 0,
            CloseTime = _marketClock.GetTimestamp(),
            Profit = realizedProfit
        });
        _marginUsed = CalculateMarginUsed();

        _ = Task.Run(() => ClosePositionAsync(worstPos.Ticket)).ContinueWith(t =>
        {
            if (t.IsFaulted) Trace.TraceError($"Stop‑out close failed: {t.Exception}");
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private void RecordTelemetry(bool success, long latencyMs)
    {
        if (!success) _metrics.RecordLiveOrderRejection();
        _metrics.RecordLiveOrderLatency(latencyMs);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await EndSessionAsync().ConfigureAwait(false);
        if (_lifecycleCts != null)
        {
            await _lifecycleCts.CancelAsync().ConfigureAwait(false);
            _lifecycleCts.Dispose();
        }
        await _adapter.DisconnectAsync().ConfigureAwait(false);
        _stateLock.Dispose();
        _syncGate.Dispose();
    }
}
