// -----------------------------------------------------------------------------
// <copyright file="LiveBroker.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

#pragma warning disable CA1031

using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Slots;
using Chronos.Core.Kernel.Clock;
using Chronos.Core.Kernel.Events;
using Chronos.Core.Kernel.Hooks;
using Chronos.Core.Kernel.Messaging;
using Chronos.Core.Kernel.Telemetry;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Chronos.Core.Kernel.Brokers;
#pragma warning disable CS1591

/// <summary>
/// Live broker wrapping an <see cref="IAdapterCapability"/> for real exchange trading.
/// All market‑time calculations are tick‑driven; wall‑clock is used only for in‑flight guards and telemetry.
/// Supports cross‑currency conversion if the adapter provides an <see cref="ICurrencyConverter"/>.
/// </summary>
public sealed class LiveBroker : IBroker, IAsyncDisposable
{
    private readonly IAdapterCapability _adapter;
    private readonly int _magicNumber;
    private readonly double _leverage;
    private readonly IMessageBus? _messageBus;
    private readonly TickClock _marketClock;
    private readonly SystemClock _wallClock;
    private readonly ICoreMetrics _metrics;
    private readonly int _orderGuardTimeoutSeconds;
    private readonly double _stopOutLevel;
    private readonly ILiveHooks? _hooks;
    private readonly long _syncIntervalTicks;
    private readonly ILogger<LiveBroker> _logger;
    private readonly ICurrencyConverter? _currencyConverter;
    private readonly string? _accountCurrency;

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private CancellationTokenSource? _lifecycleCts;
    private long _orderSequence;
    private bool _isDisposing;

    private readonly List<Position> _openPositions = new();
    private readonly List<Position> _history = new();
    private readonly List<Order> _pendingOrders = new();
    private readonly Dictionary<long, Position> _positionMap = new();
    private readonly ConcurrentDictionary<string, long> _inFlightOps = new();
    private readonly HashSet<long> _liquidationTickets = new();

    private readonly Dictionary<string, SymbolProperties> _symbolSpecs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (double Bid, double Ask)> _lastPrices = new(StringComparer.OrdinalIgnoreCase);

    private double _balance;
    private double _equity;
    private double _marginUsed;
    private double _peakEquity;
    private double _peakDailyEquity;
    private long _lastSyncTime;
    private long _nextHoldingCostTime;

    private readonly TimeSpan _reconnectBaseDelay = TimeSpan.FromSeconds(2);
    private int _reconnectAttempt;
    private bool _reconnectRunning;

    // LoggerMessage delegates
    private static readonly Action<ILogger, string, Exception?> _logCloseFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 1000, "Close failed for ticket {Ticket}");
    private static readonly Action<ILogger, string, Exception?> _logConversionFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, 1001, "Failed to get conversion rate for {Symbol}, using 1.0");
    private static readonly Action<ILogger, Exception?> _logSyncFailed =
        LoggerMessage.Define(LogLevel.Error, 1002, "Periodic sync failed, will retry on next interval");
    private static readonly Action<ILogger, Exception?> _logEndSessionCloseError =
        LoggerMessage.Define(LogLevel.Error, 1003, "EndSessionAsync: One or more close tasks failed");
    private static readonly Action<ILogger, string, int, Exception?> _logStopOutRetryFailed =
        LoggerMessage.Define<string, int>(LogLevel.Error, 1004, "Stop-out close failed for ticket {Ticket} after {Attempts} attempts");

    public double Balance => GetState(() => _balance);
    public double Equity => GetState(() => _equity);
    public double MarginUsed => GetState(() => _marginUsed);
    public double FreeMargin => GetState(() => _equity - _marginUsed);
    public double MaxDrawdown { get; private set; }
    public double MaxDailyDrawdown { get; private set; }
    public bool IsWarmup => false;

    public LiveBroker(
        IAdapterCapability adapter,
        int magicNumber,
        double leverage,
        TickClock marketClock,
        SystemClock wallClock,
        ICoreMetrics metrics,
        IMessageBus? messageBus = null,
        ILiveHooks? hooks = null,
        int orderGuardTimeoutSeconds = 5,
        double stopOutLevel = 0.50,
        long syncIntervalTicks = TimeSpan.TicksPerMinute,
        ILogger<LiveBroker>? logger = null,
        ICurrencyConverter? currencyConverter = null,
        string? accountCurrency = null)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _magicNumber = magicNumber;
        _leverage = leverage;
        _stopOutLevel = stopOutLevel;
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _messageBus = messageBus;
        _marketClock = marketClock ?? throw new ArgumentNullException(nameof(marketClock));
        _wallClock = wallClock ?? throw new ArgumentNullException(nameof(wallClock));
        _orderGuardTimeoutSeconds = orderGuardTimeoutSeconds;
        _syncIntervalTicks = syncIntervalTicks;
        _hooks = hooks;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currencyConverter = currencyConverter;
        _accountCurrency = accountCurrency;

        _adapter.OnExecutionUpdate += report =>
            _ = Task.Factory.StartNew(() => HandleExecutionReportAsync(report),
                                      CancellationToken.None,
                                      TaskCreationOptions.DenyChildAttach,
                                      TaskScheduler.Default)
                .ContinueWith(t => _logCloseFailed(_logger, "HandleExecutionReport", t.Exception),
                              CancellationToken.None,
                              TaskContinuationOptions.OnlyOnFaulted,
                              TaskScheduler.Default);
    }

    public async Task SetSymbolSpecsAsync(string symbol, SymbolProperties specs)
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try { _symbolSpecs[symbol] = specs; }
        finally { _stateLock.Release(); }
    }

    public async Task InitializeLiveStateAsync(CancellationToken cancellationToken)
    {
        await ReconcileAsync(cancellationToken).ConfigureAwait(false);
        _lifecycleCts?.Dispose();
        _lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Factory.StartNew(() => PeriodicSyncLoopAsync(_lifecycleCts.Token),
                                  CancellationToken.None,
                                  TaskCreationOptions.DenyChildAttach,
                                  TaskScheduler.Default);
        _ = Task.Factory.StartNew(() => StartReconnectionLoopAsync(_lifecycleCts.Token),
                                  CancellationToken.None,
                                  TaskCreationOptions.DenyChildAttach,
                                  TaskScheduler.Default);
    }

    private async Task PeriodicSyncLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromTicks(_syncIntervalTicks), ct).ConfigureAwait(false);
                await SyncStateAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logSyncFailed(_logger, ex);
            }
        }
    }

    private async Task StartReconnectionLoopAsync(CancellationToken ct)
    {
        if (_reconnectRunning)
        {
            return;
        }

        _reconnectRunning = true;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!_adapter.IsConnected)
                {
                    _reconnectAttempt++;
                    _hooks?.OnReconnectAttempt.InvokeActionChain(
                        _reconnectAttempt,
                        new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                            _adapter.IsConnected, "live.reconnect.attempt"));

                    bool success = await _adapter.ConnectAsync(ct).ConfigureAwait(false);
                    if (success)
                    {
                        _metrics.SetConnectionState(true, _adapter.Name);
                        _messageBus?.Publish(new ConnectionStateEvent(true, _adapter.Name)
                        {
                            Timestamp = _wallClock.GetUtcNow()
                        });
                        _hooks?.OnReconnectSuccess.InvokeActionChain(
                            _reconnectAttempt,
                            new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                                true, "live.reconnect.success"));
                        _reconnectAttempt = 0;
                        await ReconcileAsync(ct).ConfigureAwait(false);
                    }
                    else
                    {
                        double delay = Math.Min(60, _reconnectBaseDelay.TotalSeconds * Math.Pow(1.5, _reconnectAttempt));
                        await Task.Delay(TimeSpan.FromSeconds(delay), ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    _reconnectAttempt = 0;
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                }
            }
        }
        finally { _reconnectRunning = false; }
    }

    public async Task SyncStateAsync(CancellationToken cancellationToken)
    {
        if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _hooks?.OnSyncBefore.InvokeActionChain(
                    new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                        _adapter.IsConnected, "live.sync.before"));

                var (balance, equity) = await _adapter.GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
                _balance = balance;
                _equity = equity;
                if (_peakEquity == 0)
                {
                    _peakEquity = _peakDailyEquity = equity;
                }

                var active = await _adapter.GetActivePositionsAsync().ConfigureAwait(false);
                _openPositions.Clear();
                _positionMap.Clear();
                foreach (var p in active) { _openPositions.Add(p); _positionMap[p.Ticket] = p; }

                var pending = await _adapter.GetPendingOrdersAsync().ConfigureAwait(false);
                _pendingOrders.Clear();
                _pendingOrders.AddRange(pending);

                _marginUsed = CalculateTotalMarginUsed();
                UpdateDrawdowns();
                _lastSyncTime = _marketClock.GetTimestamp();

                _hooks?.OnSyncAfter.InvokeActionChain(
                    new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                        _adapter.IsConnected, "live.sync.after"));
            }
            finally { _stateLock.Release(); }
        }
        finally { _syncGate.Release(); }
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        await _syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
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
                            if (idx >= 0) { _openPositions[idx] = updated; _positionMap[ap.Ticket] = updated; }
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
                _marginUsed = CalculateTotalMarginUsed();
            }
            finally { _stateLock.Release(); }
        }
        finally { _syncGate.Release(); }
    }

    public async Task<AdapterOrderResponse> ExecuteMarketOrderAsync(
        string symbol, OrderType type, double volume, double sl = 0, double tp = 0, string comment = "")
    {
        var request = new AdapterOrderRequest
        {
            Symbol = symbol,
            Type = type,
            Volume = volume,
            StopLoss = sl,
            TakeProfit = tp,
            Comment = comment,
            MagicNumber = _magicNumber
        };

        if (!InvokeOrderValidationFilter(request, out var filtered, out var rejectReason))
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = rejectReason ?? "Rejected by filter" };
        }

        if (!InvokeOrderBeforeSendFilter(filtered, out filtered, out rejectReason))
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = rejectReason ?? "Rejected by filter" };
        }

        string key = NextOrderKey(symbol);
        if (!TryAcquireInFlight(key))
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Duplicate in‑flight order" };
        }

        if (!_lastPrices.TryGetValue(symbol, out var px) || !_symbolSpecs.TryGetValue(symbol, out var spec))
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Price or symbol properties not available" };
        }

        double refPrice = type == OrderType.Buy ? px.Ask : px.Bid;
        double requiredMargin = _adapter.Calculator.CalculateRequiredMargin(spec, refPrice, volume, _leverage);
        double conversionRate = GetConversionRate(symbol);
        double requiredMarginAccount = requiredMargin * conversionRate;

        bool marginOk;
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try { marginOk = (_equity - _marginUsed) >= requiredMarginAccount - 1e-8; }
        finally { _stateLock.Release(); }
        if (!marginOk)
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Insufficient margin" };
        }

        var sw = Stopwatch.StartNew();
        var response = await _adapter.ExecuteOrderAsync(filtered).ConfigureAwait(false);
        sw.Stop();
        RecordTelemetry(response.Success, sw.ElapsedMilliseconds);

        if (response.Success)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try { _marginUsed += requiredMarginAccount; UpdateDrawdowns(); }
            finally { _stateLock.Release(); }
        }

        InvokeOrderExecutedHook(filtered, response);
        return response;
    }

    public async Task<AdapterOrderResponse> PlacePendingOrderAsync(
        string symbol, OrderType type, double volume, double price, double sl, double tp, string comment = "")
    {
        var request = new AdapterOrderRequest
        {
            Symbol = symbol,
            Type = type,
            Volume = volume,
            Price = price,
            StopLoss = sl,
            TakeProfit = tp,
            Comment = comment,
            MagicNumber = _magicNumber
        };

        if (!InvokeOrderValidationFilter(request, out var filtered, out var rejectReason))
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = rejectReason ?? "Rejected by filter" };
        }

        if (!InvokeOrderBeforeSendFilter(filtered, out filtered, out rejectReason))
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = rejectReason ?? "Rejected by filter" };
        }

        string key = NextOrderKey(symbol);
        if (!TryAcquireInFlight(key))
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Duplicate in‑flight order" };
        }

        if (!_symbolSpecs.TryGetValue(symbol, out var spec))
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Symbol properties not available" };
        }

        double requiredMargin = _adapter.Calculator.CalculateRequiredMargin(spec, price, volume, _leverage);
        double conversionRate = GetConversionRate(symbol);
        double requiredMarginAccount = requiredMargin * conversionRate;

        bool marginOk;
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try { marginOk = (_equity - _marginUsed) >= requiredMarginAccount - 1e-8; }
        finally { _stateLock.Release(); }
        if (!marginOk)
        {
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Insufficient margin" };
        }

        var sw = Stopwatch.StartNew();
        var response = await _adapter.ExecuteOrderAsync(filtered).ConfigureAwait(false);
        sw.Stop();
        RecordTelemetry(response.Success, sw.ElapsedMilliseconds);

        if (response.Success)
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try { _marginUsed += requiredMarginAccount; UpdateDrawdowns(); }
            finally { _stateLock.Release(); }
        }

        InvokeOrderExecutedHook(filtered, response);
        return response;
    }

    public Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null)
        => _adapter.ModifyOrderAsync(ticket, sl, tp, price);

    public Task<AdapterOrderResponse> CancelOrderAsync(long ticket)
        => _adapter.CancelAsync(ticket);

    public async Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double volume = 0)
    {
        TryAcquireInFlight($"CLOSE_{ticket}");
        var sw = Stopwatch.StartNew();
        var response = await _adapter.ClosePositionAsync(ticket, volume > 0 ? volume : null).ConfigureAwait(false);
        sw.Stop();
        RecordTelemetry(response.Success, sw.ElapsedMilliseconds);
        return response;
    }

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
        return tasks.ConvertAll(t => t.Result);
    }

    public async Task<bool> HasOpenPositionAsync(string symbol, OrderType? type = null, CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return _openPositions.Any(p => p.Symbol == symbol && (type == null || p.Type == type)); }
        finally { _stateLock.Release(); }
    }

    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return symbol == null ? [.. _openPositions] : _openPositions.Where(p => p.Symbol == symbol).ToList(); }
        finally { _stateLock.Release(); }
    }

    public async Task<IReadOnlyList<Position>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return _history.ToList(); }
        finally { _stateLock.Release(); }
    }

    public async Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return _pendingOrders.ToList(); }
        finally { _stateLock.Release(); }
    }

    public async Task OnTickAsync(string symbol, Tick tick)
    {
        _marketClock.SetTickTime(tick.Time);
        long now = _marketClock.GetTimestamp();

        if (_hooks != null)
        {
            var ctx = new LiveContext(_wallClock, tick, _equity, _balance, MaxDrawdown, this, _adapter.Name,
                _adapter.IsConnected, "live.tick.received");
            var filterResult = _hooks.OnTickReceived.InvokeFilterChain(tick, ctx);
            if (!filterResult.IsAllowed)
            {
                return;
            }

            tick = filterResult.IsAllowed ? filterResult.Data : tick;
        }

        long latencyTicks = _wallClock.GetUtcNow().Ticks - now;
        _metrics.RecordLiveTickLatency(latencyTicks);

        if (_syncIntervalTicks > 0 && (now - _lastSyncTime) >= _syncIntervalTicks)
        {
            await SyncStateAsync(CancellationToken.None).ConfigureAwait(false);
        }

        List<long> ticketsToClose = new();
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
                if (!_symbolSpecs.TryGetValue(p.Symbol, out var spec))
                {
                    continue;
                }

                double currentPx = p.Type == OrderType.Buy ? bid : ask;
                double rawPnl = _adapter.Calculator.CalculatePnL(spec, p.OpenPrice, currentPx, p.Volume, p.Type);
                double conversionRate = GetConversionRate(p.Symbol);
                double pnlInAccount = rawPnl * conversionRate;
                var updated = p with { Profit = pnlInAccount - p.Commission + p.Swap };
                _openPositions[i] = updated;
                _positionMap[p.Ticket] = updated;
                floatPl += updated.Profit;

                if (p.Symbol == symbol)
                {
                    bool close = false;
                    if (p.Type == OrderType.Buy)
                    {
                        if (p.TP > 0 && bid >= p.TP)
                        {
                            close = true;
                        }
                        else if (p.SL > 0 && bid <= p.SL)
                        {
                            close = true;
                        }
                    }
                    else
                    {
                        if (p.TP > 0 && ask <= p.TP)
                        {
                            close = true;
                        }
                        else if (p.SL > 0 && ask >= p.SL)
                        {
                            close = true;
                        }
                    }
                    if (close)
                    {
                        ticketsToClose.Add(p.Ticket);
                    }
                }
            }

            _equity = _balance + floatPl;
            UpdateDrawdowns();

            double totalMargin = CalculateActiveMarginUsed();
            if (totalMargin > 0 && _stopOutLevel > 0 && (_equity / totalMargin) <= _stopOutLevel)
            {
                ApplyStopOut();
            }

            InvokeEquityChangedHook();
        }
        finally { _stateLock.Release(); }

        foreach (var tck in ticketsToClose)
        {
            try { await ClosePositionAsync(tck).ConfigureAwait(false); }
            catch (Exception ex) { _logCloseFailed(_logger, tck.ToString(CultureInfo.InvariantCulture), ex); }
        }

        _hooks?.OnTickProcessed.InvokeActionChain(
            tick,
            new LiveContext(_wallClock, tick, _equity, _balance, MaxDrawdown, this, _adapter.Name, _adapter.IsConnected,
                "live.tick.processed"));
    }

    public async Task ConnectAndNotifyAsync(CancellationToken ct)
    {
        bool connected = await _adapter.ConnectAsync(ct).ConfigureAwait(false);
        if (connected)
        {
            _metrics.SetConnectionState(true, _adapter.Name);
            _messageBus?.Publish(new ConnectionStateEvent(true, _adapter.Name)
            {
                Timestamp = _wallClock.GetUtcNow()
            });
            _hooks?.OnStart.InvokeActionChain(
                new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                    true, "live.start"));
        }
    }

    public async Task DisconnectAndNotifyAsync()
    {
        _hooks?.OnStop.InvokeActionChain(
            new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                _adapter.IsConnected, "live.stop"));
        await _adapter.DisconnectAsync().ConfigureAwait(false);
        _metrics.SetConnectionState(false, _adapter.Name);
        _messageBus?.Publish(new ConnectionStateEvent(false, _adapter.Name)
        {
            Timestamp = _wallClock.GetUtcNow()
        });
    }

    public async ValueTask DisposeAsync()
    {
        _hooks?.OnStop.InvokeActionChain(
            new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                _adapter.IsConnected, "live.stop"));
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

    private async Task EndSessionAsync()
    {
        List<Position> positionsToClose;
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _isDisposing = true;
            positionsToClose = _openPositions.ToList();
        }
        finally { _stateLock.Release(); }

        var closeTasks = positionsToClose.Select(p => ClosePositionAsync(p.Ticket));
        try { await Task.WhenAll(closeTasks).ConfigureAwait(false); }
        catch (Exception ex) { _logEndSessionCloseError(_logger, ex); }

        _messageBus?.Publish(new LiveSessionEndedEvent
        {
            FinalBalance = _balance,
            FinalEquity = _equity,
            MaxDrawdownPct = MaxDrawdown,
            MaxDailyDrawdownPct = MaxDailyDrawdown,
            TotalTrades = _history.Count,
            CorrelationId = Guid.NewGuid(),
            Timestamp = _wallClock.GetUtcNow()
        });
    }

    private string NextOrderKey(string symbol) => $"{symbol}_ORD_{Interlocked.Increment(ref _orderSequence)}";

    private bool TryAcquireInFlight(string key)
    {
        long now = _wallClock.GetTimestamp();
        if (_inFlightOps.TryGetValue(key, out long expiry) && now < expiry)
        {
            return false;
        }

        _inFlightOps[key] = now + _orderGuardTimeoutSeconds * 1000L;
        return true;
    }

    private double CalculateActiveMarginUsed()
    {
        double used = 0;
        foreach (var pos in _openPositions)
        {
            if (_symbolSpecs.TryGetValue(pos.Symbol, out var spec))
            {
                double margin = _adapter.Calculator.CalculateRequiredMargin(spec, pos.OpenPrice, pos.Volume, _leverage);
                double conv = GetConversionRate(pos.Symbol);
                used += margin * conv;
            }
        }
        return used;
    }

    private double CalculateTotalMarginUsed()
    {
        double used = CalculateActiveMarginUsed();
        foreach (var o in _pendingOrders)
        {
            if (_symbolSpecs.TryGetValue(o.Symbol, out var spec))
            {
                double margin = _adapter.Calculator.CalculateRequiredMargin(spec, o.Price, o.Volume, _leverage);
                double conv = GetConversionRate(o.Symbol);
                used += margin * conv;
            }
        }
        return used;
    }

    private async Task HandleExecutionReportAsync(ExecutionReport report)
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isDisposing)
            {
                return;
            }

            if (report.State is not (ExecutionState.Filled or ExecutionState.PartiallyFilled))
            {
                return;
            }

            _liquidationTickets.Remove(report.Ticket);

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

                    _hooks?.OnPositionClosed.InvokeActionChain(
                        closedPosition,
                        new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                            _adapter.IsConnected, "live.position.closed"));

                    _messageBus?.Publish(new OrderExecutedEvent
                    {
                        Symbol = pos.Symbol,
                        OrderType = pos.Type.ToString(),
                        Volume = report.ExecutedVolume,
                        Price = report.ExecutedPrice,
                        IsOpen = false,
                        CorrelationId = Guid.NewGuid(),
                        Timestamp = _marketClock.GetUtcNow()
                    });
                }
                else
                {
                    double newVolume = pos.Volume + (report.Type == pos.Type ? report.ExecutedVolume : -report.ExecutedVolume);
                    if (_symbolSpecs.TryGetValue(pos.Symbol, out var spec))
                    {
                        newVolume = _adapter.Calculator.NormalizeVolume(spec, newVolume);
                    }

                    var updated = pos with { Volume = newVolume };
                    int idx = _openPositions.IndexOf(pos);
                    if (idx >= 0) { _openPositions[idx] = updated; _positionMap[report.Ticket] = updated; }
                }
            }
            else if (!_isDisposing)
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

                _hooks?.OnPositionOpened.InvokeActionChain(
                    newPos,
                    new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                        _adapter.IsConnected, "live.position.opened"));

                _messageBus?.Publish(new OrderExecutedEvent
                {
                    Symbol = report.Symbol,
                    OrderType = report.Type.ToString(),
                    Volume = report.ExecutedVolume,
                    Price = report.ExecutedPrice,
                    IsOpen = true,
                    CorrelationId = Guid.NewGuid(),
                    Timestamp = _marketClock.GetUtcNow()
                });
            }

            _marginUsed = CalculateTotalMarginUsed();
        }
        finally { _stateLock.Release(); }
    }

    private void ApplyStopOut()
    {
        if (_openPositions.Count == 0)
        {
            return;
        }

        int worstIdx = -1;
        double worstProfit = double.MaxValue;
        for (int i = 0; i < _openPositions.Count; i++)
        {
            var p = _openPositions[i];
            if (_liquidationTickets.Contains(p.Ticket))
            {
                continue;
            }

            if (p.Profit < worstProfit) { worstProfit = p.Profit; worstIdx = i; }
        }

        if (worstIdx < 0)
        {
            return; // all positions already being liquidated
        }

        var worstPos = _openPositions[worstIdx];
        _liquidationTickets.Add(worstPos.Ticket);

        _hooks?.OnPositionStopout.InvokeActionChain(
            worstPos,
            new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                _adapter.IsConnected, "live.position.stopout"));

        _ = Task.Factory.StartNew(async () =>
        {
            int attempts = 0;
            const int maxAttempts = 3;
            Exception? lastEx = null;
            while (attempts < maxAttempts)
            {
                try
                {
                    await ClosePositionAsync(worstPos.Ticket).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    attempts++;
                    if (attempts < maxAttempts)
                    {
                        int delay = (int)Math.Pow(2, attempts) * 1000;
                        await Task.Delay(delay, CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
            _logStopOutRetryFailed(_logger, worstPos.Ticket.ToString(CultureInfo.InvariantCulture), attempts, lastEx);
        }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
        .ContinueWith(t =>
        {
            lock (_liquidationTickets)
            {
                _liquidationTickets.Remove(worstPos.Ticket);
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void ProcessHoldingCosts()
    {
        long currentTime = _marketClock.GetTimestamp();
        long holdingInterval = _symbolSpecs.Values
            .Select(s => s.HoldingCostIntervalTicks)
            .DefaultIfEmpty(TimeSpan.TicksPerDay)
            .Min();

        if (_nextHoldingCostTime == 0)
        {
            _nextHoldingCostTime = (currentTime / holdingInterval + 1) * holdingInterval;
            _peakDailyEquity = _equity;
            return;
        }

        while (currentTime >= _nextHoldingCostTime)
        {
            long prevBoundary = _nextHoldingCostTime - holdingInterval;
            for (int i = 0; i < _openPositions.Count; i++)
            {
                var pos = _openPositions[i];
                if (_symbolSpecs.TryGetValue(pos.Symbol, out var spec))
                {
                    double cost = _adapter.Calculator.CalculateHoldingCost(
                        spec, pos.Volume, pos.OpenPrice, pos.Type, prevBoundary, _nextHoldingCostTime);
                    double conv = GetConversionRate(pos.Symbol);
                    if (Math.Abs(cost) > 0)
                    {
                        var updated = pos with { Swap = pos.Swap + cost * conv };
                        _openPositions[i] = updated;
                        _positionMap[pos.Ticket] = updated;
                    }
                }
            }

            DateTime utcCurrent = new DateTime(_nextHoldingCostTime, DateTimeKind.Utc).Date;
            DateTime utcLast = new DateTime(prevBoundary, DateTimeKind.Utc).Date;
            if (utcCurrent != utcLast)
            {
                _peakDailyEquity = _equity;
            }

            _nextHoldingCostTime += holdingInterval;
        }
    }

    private void UpdateDrawdowns()
    {
        if (_equity > _peakEquity)
        {
            _peakEquity = _equity;
        }

        if (_peakEquity > 1e-8)
        {
            double dd = (_peakEquity - _equity) / _peakEquity * 100.0;
            if (dd > MaxDrawdown)
            {
                MaxDrawdown = dd;
            }
        }

        if (_equity > _peakDailyEquity)
        {
            _peakDailyEquity = _equity;
        }

        if (_peakDailyEquity > 1e-8)
        {
            double dailyDd = (_peakDailyEquity - _equity) / _peakDailyEquity * 100.0;
            if (dailyDd > MaxDailyDrawdown)
            {
                MaxDailyDrawdown = dailyDd;
            }
        }
    }

    private void InvokeEquityChangedHook()
    {
        if (_hooks == null)
        {
            return;
        }

        var snapshot = new EquitySnapshot(_equity, _balance, MaxDrawdown, MaxDailyDrawdown);
        _hooks.OnEquityChanged.InvokeActionChain(
            snapshot,
            new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                _adapter.IsConnected, "live.equity.changed"));
    }

    private bool InvokeOrderValidationFilter(AdapterOrderRequest request, out AdapterOrderRequest filtered, out string? reason)
    {
        filtered = request; reason = null;
        if (_hooks == null)
        {
            return true;
        }

        var ctx = new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
            _adapter.IsConnected, "live.order.validation");
        var result = _hooks.OnOrderValidation.InvokeFilterChain(request, ctx);
        filtered = result.Data ?? request;
        reason = result.RejectionReason;
        return result.IsAllowed;
    }

    private bool InvokeOrderBeforeSendFilter(AdapterOrderRequest request, out AdapterOrderRequest filtered, out string? reason)
    {
        filtered = request; reason = null;
        if (_hooks == null)
        {
            return true;
        }

        var ctx = new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
            _adapter.IsConnected, "live.order.before_send");
        var result = _hooks.OnOrderBeforeSend.InvokeFilterChain(request, ctx);
        filtered = result.Data ?? request;
        reason = result.RejectionReason;
        return result.IsAllowed;
    }

    private void InvokeOrderExecutedHook(AdapterOrderRequest request, AdapterOrderResponse response)
    {
        if (_hooks == null)
        {
            return;
        }

        if (response.Success)
        {
            _hooks.OnOrderExecuted.InvokeActionChain(
                new ExecutionReport
                {
                    Ticket = response.Ticket,
                    Symbol = request.Symbol,
                    Type = request.Type,
                    State = ExecutionState.Filled,
                    ExecutedVolume = response.ExecutedVolume,
                    ExecutedPrice = response.ExecutedPrice,
                    Comment = request.Comment,
                    Timestamp = _marketClock.GetTimestamp()
                },
                new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                    _adapter.IsConnected, "live.order.executed"));
        }
        else
        {
            _hooks.OnOrderRejected.InvokeActionChain(
                (request, response.ErrorMessage),
                new LiveContext(_wallClock, new Tick(), _equity, _balance, MaxDrawdown, this, _adapter.Name,
                    _adapter.IsConnected, "live.order.rejected"));
        }
    }

    private void RecordTelemetry(bool success, long latencyMs)
    {
        if (!success)
        {
            _metrics.RecordLiveOrderRejection();
        }

        _metrics.RecordLiveOrderLatency(latencyMs);
    }

    private double GetConversionRate(string symbol)
    {
        if (_currencyConverter == null || string.IsNullOrEmpty(_accountCurrency))
        {
            return 1.0;
        }

        try
        {
            return _currencyConverter.GetConversionRateAsync(symbol, _accountCurrency, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logConversionFailed(_logger, symbol, ex);
            return 1.0;
        }
    }

    private double GetState(Func<double> accessor)
    {
        _stateLock.Wait();
        try { return accessor(); }
        finally { _stateLock.Release(); }
    }
}
#pragma warning restore CS1591
