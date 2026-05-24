using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Strategies;
using Chronos.Core.Abstractions.Shared.Events;
using Chronos.Core.Kernel.Clock;

namespace Chronos.Core.Kernel.Brokers;

/// <summary>
/// Deterministic simulated broker for backtesting and optimisation.
/// Maintains all state internally using mutable objects and exposes immutable position snapshots.
/// No external I/O, no configuration builders – all parameters are constructor‑injected.
/// </summary>
public sealed class SimulatedBroker : IBroker
{
    private readonly IMarketCalculator _calculator;
    private readonly ISimulationFriction _friction;
    private readonly Dictionary<string, SymbolProperties> _symbolSpecs;
    private readonly double _leverage;
    private readonly long _latencyTicks;
    private readonly int _maxOpenPositions;
    private readonly double _stopOutLevel;
    private readonly IMessageBus? _messageBus;
    private readonly TickClock _clock;
    private static long _eventCounter;

    private readonly Lock _stateLock = new();

    private long _ticketCounter = 1;
    private double _balance;
    private double _equity;
    private double _marginUsed;

    private readonly List<MutablePosition> _positions = new(100);
    private readonly List<MutablePosition> _history = new(1000);
    private readonly List<Order> _pendingOrders = new(50);
    private readonly Dictionary<string, (double Bid, double Ask)> _marketPrices = [];
    private readonly Queue<QueuedMarketOrder> _executionQueue = new(10);

    private long _nextHoldingCostTime;
    private double _peakEquity;
    private double _peakDailyEquity;

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

    /// <summary>
    /// Initialises a new simulated broker.
    /// </summary>
    /// <param name="calculator">Exchange‑specific financial math.</param>
    /// <param name="friction">Slippage and commission model.</param>
    /// <param name="symbolSpecs">Symbol properties (tick size, contract size, etc.).</param>
    /// <param name="initialBalance">Starting balance.</param>
    /// <param name="leverage">Leverage multiplier.</param>
    /// <param name="clock">Tick‑driven clock for all market operations.</param>
    /// <param name="latencyTicks">Execution delay in ticks. 0 = instant.</param>
    /// <param name="maxOpenPositions">Max concurrent positions.</param>
    /// <param name="stopOutLevel">Stop‑out margin level ratio (e.g., 0.5).</param>
    /// <param name="messageBus">Optional message bus for event publication.</param>
    public SimulatedBroker(
        IMarketCalculator calculator,
        ISimulationFriction friction,
        Dictionary<string, SymbolProperties> symbolSpecs,
        double initialBalance,
        double leverage,
        TickClock clock,
        long latencyTicks = 0,
        int maxOpenPositions = 100,
        double stopOutLevel = 0.50,
        IMessageBus? messageBus = null)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _friction = friction ?? throw new ArgumentNullException(nameof(friction));
        _symbolSpecs = symbolSpecs ?? throw new ArgumentNullException(nameof(symbolSpecs));
        _balance = initialBalance;
        _equity = initialBalance;
        _peakEquity = initialBalance;
        _peakDailyEquity = initialBalance;
        _leverage = leverage;
        _latencyTicks = Math.Max(0, latencyTicks);
        _maxOpenPositions = maxOpenPositions;
        _stopOutLevel = stopOutLevel;
        _messageBus = messageBus;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc/>
    public Task InitializeLiveStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task SyncStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task OnTickAsync(string symbol, Tick tick)
    {
        if (tick.Time < 0 || tick.Time > DateTime.MaxValue.Ticks) return Task.CompletedTask;

        _clock.SetTickTime(tick.Time);
        lock (_stateLock)
        {
            _marketPrices[symbol] = (tick.Bid, tick.Ask);
            ProcessHoldingCosts();

            if (_executionQueue.Count > 0) ProcessExecutionQueue();
            if (_pendingOrders.Count > 0) CheckPendingOrders(symbol, tick.Bid, tick.Ask);
            if (_positions.Count > 0) UpdatePositions(symbol, tick.Bid, tick.Ask);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<AdapterOrderResponse> ExecuteMarketOrderAsync(string symbol, OrderType type, double volume,
        double sl = 0, double tp = 0, string comment = "")
    {
        lock (_stateLock)
        {
            if (_positions.Count >= _maxOpenPositions)
                return Task.FromResult(new AdapterOrderResponse
                    { Success = false, ErrorMessage = "Max open positions reached" });

            if (_latencyTicks > 0 && _clock.GetTimestamp() > 0)
            {
                _executionQueue.Enqueue(new QueuedMarketOrder
                {
                    ExecutionTime = _clock.GetTimestamp() + _latencyTicks,
                    Symbol = symbol,
                    Type = type,
                    Volume = volume,
                    SL = sl,
                    TP = tp,
                    Comment = comment
                });
                return Task.FromResult(new AdapterOrderResponse { Success = true, Ticket = _ticketCounter++ });
            }

            return Task.FromResult(ExecuteInstantly(symbol, type, volume, sl, tp, comment));
        }
    }

    /// <inheritdoc/>
    public Task<AdapterOrderResponse> PlacePendingOrderAsync(string symbol, OrderType type, double volume,
        double price, double sl, double tp, string comment = "")
    {
        lock (_stateLock)
        {
            // Margin check (parity with LiveBroker).
            if (!_symbolSpecs.TryGetValue(symbol, out var spec))
            {
                return Task.FromResult(new AdapterOrderResponse
                    { Success = false, ErrorMessage = "Symbol properties not available" });
            }

            double requiredMargin = _calculator.CalculateRequiredMargin(spec, price, volume, _leverage);
            if (FreeMargin < requiredMargin - 1e-8)
            {
                return Task.FromResult(new AdapterOrderResponse
                    { Success = false, ErrorMessage = "Insufficient margin" });
            }

            var ticket = _ticketCounter++;
            _pendingOrders.Add(new Order
            {
                Ticket = ticket, Symbol = symbol, Type = type, Volume = volume, Price = price, SL = sl, TP = tp,
                Comment = comment
            });
            return Task.FromResult(new AdapterOrderResponse { Success = true, Ticket = ticket });
        }
    }

    /// <inheritdoc/>
    public Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null,
        double? price = null)
    {
        lock (_stateLock)
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                if (_positions[i].Ticket == ticket)
                {
                    if (price.HasValue)
                        return Task.FromResult(new AdapterOrderResponse
                            { Success = false, ErrorMessage = "Cannot modify price of an open position" });
                    if (sl.HasValue) _positions[i].SL = sl.Value;
                    if (tp.HasValue) _positions[i].TP = tp.Value;
                    return Task.FromResult(new AdapterOrderResponse { Success = true });
                }
            }

            for (int i = 0; i < _pendingOrders.Count; i++)
            {
                if (_pendingOrders[i].Ticket == ticket)
                {
                    var o = _pendingOrders[i];
                    if (price.HasValue) o = o with { Price = price.Value };
                    if (sl.HasValue) o = o with { SL = sl.Value };
                    if (tp.HasValue) o = o with { TP = tp.Value };
                    _pendingOrders[i] = o;
                    return Task.FromResult(new AdapterOrderResponse { Success = true });
                }
            }
        }

        return Task.FromResult(new AdapterOrderResponse { Success = false, ErrorMessage = "Ticket not found" });
    }

    /// <inheritdoc/>
    public Task<AdapterOrderResponse> CancelOrderAsync(long ticket)
    {
        lock (_stateLock)
        {
            int removed = _pendingOrders.RemoveAll(o => o.Ticket == ticket);
            return Task.FromResult(new AdapterOrderResponse
            {
                Success = removed > 0,
                ErrorMessage = removed > 0 ? string.Empty : "Ticket not found among pending orders"
            });
        }
    }

    /// <inheritdoc/>
    public Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double volume = 0)
    {
        lock (_stateLock)
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                if (_positions[i].Ticket == ticket)
                {
                    if (!_marketPrices.TryGetValue(_positions[i].Symbol, out _))
                        return Task.FromResult(new AdapterOrderResponse
                            { Success = false, ErrorMessage = "Price not available" });

                    var px = _marketPrices[_positions[i].Symbol];
                    double closePx = _positions[i].Type == OrderType.Buy ? px.Bid : px.Ask;
                    double closeVol = volume > 0 ? volume : _positions[i].Volume;
                    ClosePositionInternal(i, closePx, _clock.GetTimestamp(), closeVol);
                    return Task.FromResult(new AdapterOrderResponse { Success = true });
                }
            }
        }

        return Task.FromResult(new AdapterOrderResponse { Success = false, ErrorMessage = "Ticket not found" });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AdapterOrderResponse>> CloseAllAsync(string symbol, OrderType? type = null)
    {
        var responses = new List<AdapterOrderResponse>();
        lock (_stateLock)
        {
            _pendingOrders.RemoveAll(o => o.Symbol == symbol);
            if (!_marketPrices.TryGetValue(symbol, out var px))
                return Task.FromResult<IReadOnlyList<AdapterOrderResponse>>(responses);

            for (int i = _positions.Count - 1; i >= 0; i--)
            {
                if (_positions[i].Symbol == symbol && (type == null || _positions[i].Type == type))
                {
                    double closePx = _positions[i].Type == OrderType.Buy ? px.Bid : px.Ask;
                    ClosePositionInternal(i, closePx, _clock.GetTimestamp(), _positions[i].Volume);
                    responses.Add(new AdapterOrderResponse { Success = true });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<AdapterOrderResponse>>(responses);
    }

    /// <inheritdoc/>
    public Task<bool> HasOpenPositionAsync(string symbol, OrderType? type = null,
        CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
            return Task.FromResult(_positions.Any(p => p.Symbol == symbol && (type == null || p.Type == type)));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Position>> GetOpenPositionsAsync(string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            List<MutablePosition> source =
                string.IsNullOrEmpty(symbol) ? _positions : _positions.FindAll(p => p.Symbol == symbol);
            return Task.FromResult<IReadOnlyList<Position>>([.. source.Select(ToImmutable)]);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Position>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
            return Task.FromResult<IReadOnlyList<Position>>([.. _history.Select(ToImmutable)]);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Order>> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock) return Task.FromResult<IReadOnlyList<Order>>(_pendingOrders.ToList());
    }

    // ── Private helpers ──

    private AdapterOrderResponse ExecuteInstantly(string symbol, OrderType type, double volume, double sl, double tp,
        string comment)
    {
        if (!_marketPrices.TryGetValue(symbol, out _))
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Price not available" };

        var px = _marketPrices[symbol];
        var spec = _symbolSpecs[symbol];
        double refPrice = type == OrderType.Buy ? px.Ask : px.Bid;
        double slippage = _friction.CalculateSlippage(symbol, type, volume, refPrice);
        double execPrice = type == OrderType.Buy ? refPrice + slippage : refPrice - slippage;
        execPrice = _calculator.NormalizePrice(spec, execPrice);

        double requiredMargin = _calculator.CalculateRequiredMargin(spec, execPrice, volume, _leverage);
        if (FreeMargin < requiredMargin - 1e-8)
            return new AdapterOrderResponse { Success = false, ErrorMessage = "Insufficient margin" };

        double commission = _friction.CalculateCommission(symbol, volume);
        long ticket = _ticketCounter++;

        var position = new MutablePosition
        {
            Ticket = ticket,
            Symbol = symbol,
            Type = type,
            Volume = volume,
            OpenPrice = execPrice,
            OpenTime = _clock.GetTimestamp(),
            SL = sl,
            TP = tp,
            Comment = comment,
            Commission = commission,
            Leverage = _leverage,
            AccountEquityAtOpen = _equity
        };

        _positions.Add(position);
        _marginUsed += requiredMargin;
        UpdateDrawdowns();

        _messageBus?.Publish(new OrderExecutedEvent
        {
            Symbol = symbol, OrderType = type.ToString(), Volume = volume, Price = execPrice, IsOpen = true,
            EventId = $"sim-{Interlocked.Increment(ref _eventCounter)}",
            CorrelationId = null

        });
        return new AdapterOrderResponse
            { Success = true, Ticket = ticket, ExecutedPrice = execPrice, ExecutedVolume = volume };
    }

    private void ProcessExecutionQueue()
    {
        while (_executionQueue.TryPeek(out var q) && _clock.GetTimestamp() >= q.ExecutionTime)
        {
            _executionQueue.Dequeue();
            ExecuteInstantly(q.Symbol, q.Type, q.Volume, q.SL, q.TP, q.Comment);
        }
    }

    private void CheckPendingOrders(string symbol, double bid, double ask)
    {
        for (int i = _pendingOrders.Count - 1; i >= 0; i--)
        {
            var o = _pendingOrders[i];
            if (o.Symbol != symbol) continue;
            if (_calculator.IsPendingOrderTriggered(_symbolSpecs[symbol], o.Type, bid, ask, o.Price))
            {
                ConvertPendingToPosition(o, o.Price);
                _pendingOrders.RemoveAt(i);
            }
        }
    }

    private void UpdatePositions(string symbol, double bid, double ask)
    {
        double totalFloatPl = 0, totalUsedMargin = 0;
        for (int i = _positions.Count - 1; i >= 0; i--)
        {
            var p = _positions[i];
            double currentBid = bid, currentAsk = ask;
            if (p.Symbol != symbol && _marketPrices.TryGetValue(p.Symbol, out var px))
                (currentBid, currentAsk) = px;

            var spec = _symbolSpecs[p.Symbol];
            double currentPrice = p.Type == OrderType.Buy ? currentBid : currentAsk;
            double rawPnl = _calculator.CalculatePnL(spec, p.OpenPrice, currentPrice, p.Volume, p.Type);
            p.Profit = rawPnl - p.Commission + p.Swap;

            bool closed = false;
            if (p.Symbol == symbol)
            {
                if (p.Type == OrderType.Buy)
                {
                    if (p.TP > 0 && currentBid >= p.TP) closed = true;
                    else if (p.SL > 0 && currentBid <= p.SL) closed = true;
                }
                else
                {
                    if (p.TP > 0 && currentAsk <= p.TP) closed = true;
                    else if (p.SL > 0 && currentAsk >= p.SL) closed = true;
                }
            }

            if (closed)
                ClosePositionInternal(i, p.Type == OrderType.Buy ? currentBid : currentAsk, _clock.GetTimestamp(),
                    p.Volume);
            else
            {
                totalFloatPl += p.Profit;
                totalUsedMargin += _calculator.CalculateRequiredMargin(spec, p.OpenPrice, p.Volume, _leverage);
            }
        }

        _marginUsed = totalUsedMargin;
        _equity = _balance + totalFloatPl;

        if (_marginUsed > 0 && (_equity / _marginUsed) <= _stopOutLevel) ApplyStopOut(bid, ask);
        UpdateDrawdowns();
    }

    private void ClosePositionInternal(int index, double price, long time, double closeVolume)
    {
        var p = _positions[index];
        var spec = _symbolSpecs[p.Symbol];
        closeVolume = Math.Min(closeVolume, p.Volume);
        bool isPartial = Math.Abs(p.Volume - closeVolume) > 1e-8;

        double rawSlippage = _friction.CalculateSlippage(p.Symbol,
            p.Type == OrderType.Buy ? OrderType.Sell : OrderType.Buy, closeVolume, price);
        double closePx = p.Type == OrderType.Buy ? price - rawSlippage : price + rawSlippage;
        closePx = _calculator.NormalizePrice(spec, closePx);

        double closeComm = _friction.CalculateCommission(p.Symbol, closeVolume);
        double closedRawPnl = _calculator.CalculatePnL(spec, p.OpenPrice, closePx, closeVolume, p.Type);
        double closedSwap = p.Swap * (closeVolume / p.Volume);
        double realizedProfit = closedRawPnl - closeComm + closedSwap;
        _balance += realizedProfit;

        MutablePosition historyRecord;
        if (isPartial)
        {
            historyRecord = new MutablePosition
            {
                Ticket = p.Ticket,
                Symbol = p.Symbol,
                Type = p.Type,
                Volume = closeVolume,
                OpenPrice = p.OpenPrice,
                OpenTime = p.OpenTime,
                ClosePrice = closePx,
                CloseTime = time,
                Commission = closeComm,
                Swap = closedSwap,
                Profit = realizedProfit,
                SL = p.SL,
                TP = p.TP,
                Comment = p.Comment,
                AccountEquityAtOpen = p.AccountEquityAtOpen
            };
            p.Volume -= closeVolume;
            p.Commission -= closeComm;
            p.Swap -= closedSwap;
        }
        else
        {
            p.ClosePrice = closePx;
            p.CloseTime = time;
            p.Commission += closeComm;
            p.Profit = realizedProfit;
            historyRecord = p;
            _positions.RemoveAt(index);
        }

        double marginReq =
            _calculator.CalculateRequiredMargin(spec, historyRecord.OpenPrice, historyRecord.Volume, _leverage);
        historyRecord.ReturnPct = marginReq > 0 ? historyRecord.Profit / marginReq : 0;
        _history.Add(historyRecord);

        _messageBus?.Publish(new OrderExecutedEvent
        {
            Symbol = historyRecord.Symbol, OrderType = historyRecord.Type.ToString(), Volume = closeVolume,
            Price = closePx, IsOpen = false, CorrelationId = Guid.NewGuid()
        });
    }

    private void ConvertPendingToPosition(Order o, double price)
    {
        var spec = _symbolSpecs[o.Symbol];
        OrderType execDir = (o.Type == OrderType.BuyLimit || o.Type == OrderType.BuyStop)
            ? OrderType.Buy
            : OrderType.Sell;
        double reqMargin = _calculator.CalculateRequiredMargin(spec, price, o.Volume, _leverage);
        if (FreeMargin < reqMargin - 1e-8) return;

        double slippage = _friction.CalculateSlippage(o.Symbol, execDir, o.Volume, price);
        double execPx = execDir == OrderType.Buy ? price + slippage : price - slippage;
        execPx = _calculator.NormalizePrice(spec, execPx);
        double comm = _friction.CalculateCommission(o.Symbol, o.Volume);

        var newPos = new MutablePosition
        {
            Ticket = _ticketCounter++,
            Symbol = o.Symbol,
            Type = execDir,
            Volume = o.Volume,
            OpenPrice = execPx,
            OpenTime = _clock.GetTimestamp(),
            SL = o.SL,
            TP = o.TP,
            Comment = o.Comment,
            Commission = comm,
            Leverage = _leverage,
            AccountEquityAtOpen = _equity
        };
        _positions.Add(newPos);
        _marginUsed += reqMargin;
        UpdateDrawdowns();

        _messageBus?.Publish(new OrderExecutedEvent
        {
            Symbol = o.Symbol, OrderType = execDir.ToString(), Volume = o.Volume, Price = execPx, IsOpen = true,
            CorrelationId = Guid.NewGuid()
        });
    }

    private void ApplyStopOut(double bid, double ask)
    {
        _pendingOrders.Clear();
        int worstIdx = 0;
        double worstPl = _positions[0].Profit;
        for (int i = 1; i < _positions.Count; i++)
        {
            if (_positions[i].Profit < worstPl)
            {
                worstPl = _positions[i].Profit;
                worstIdx = i;
            }
        }

        var worstPos = _positions[worstIdx];
        if (!_marketPrices.TryGetValue(worstPos.Symbol, out _)) return;
        var px = _marketPrices[worstPos.Symbol];
        double closePx = worstPos.Type == OrderType.Buy ? px.Bid : px.Ask;
        ClosePositionInternal(worstIdx, closePx, _clock.GetTimestamp(), worstPos.Volume);

        double floatPl = 0, usedMargin = 0;
        foreach (var p in _positions)
        {
            floatPl += p.Profit;
            var spec = _symbolSpecs[p.Symbol];
            usedMargin += _calculator.CalculateRequiredMargin(spec, p.OpenPrice, p.Volume, _leverage);
        }

        _marginUsed = usedMargin;
        _equity = _balance + floatPl;
    }

    /// <summary>
    /// Processes daily holding costs (swap/funding) for all open positions.
    /// Daily drawdown peak is reset only upon receiving the first tick of a new UTC day,
    /// because all market‑time calculations are tick‑driven (Principle 3).
    /// </summary>
    private void ProcessHoldingCosts()
    {
        long currentTime = _clock.GetTimestamp();
        if (_nextHoldingCostTime == 0)
        {
            _nextHoldingCostTime = (currentTime / TimeSpan.TicksPerDay + 1) * TimeSpan.TicksPerDay;
            _peakDailyEquity = _equity;
            return;
        }

        if (currentTime >= _nextHoldingCostTime)
        {
            if (currentTime > 0)
            {
                foreach (var p in _positions)
                {
                    if (_symbolSpecs.TryGetValue(p.Symbol, out var spec))
                    {
                        long prevBoundary = _nextHoldingCostTime - TimeSpan.TicksPerDay;
                        double cost = _calculator.CalculateHoldingCost(spec, p.Volume, p.OpenPrice, p.Type, prevBoundary, _nextHoldingCostTime);
                        p.Swap += cost;
                    }
                }
            }

            DateTime utcCurrent = new DateTime(currentTime, DateTimeKind.Utc).Date;
            DateTime utcLast = new DateTime(currentTime - TimeSpan.TicksPerDay, DateTimeKind.Utc).Date;
            if (utcCurrent != utcLast) _peakDailyEquity = _equity;

            _nextHoldingCostTime = (currentTime / TimeSpan.TicksPerDay + 1) * TimeSpan.TicksPerDay;
        }
    }

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

    private static Position ToImmutable(MutablePosition mp) => new()
    {
        Ticket = mp.Ticket,
        Symbol = mp.Symbol,
        Type = mp.Type,
        Volume = mp.Volume,
        OpenPrice = mp.OpenPrice,
        OpenTime = mp.OpenTime,
        ClosePrice = mp.ClosePrice,
        CloseTime = mp.CloseTime,
        SL = mp.SL,
        TP = mp.TP,
        Commission = mp.Commission,
        Swap = mp.Swap,
        Profit = mp.Profit,
        ReturnPct = mp.ReturnPct,
        Comment = mp.Comment,
        Leverage = mp.Leverage,
        IsMargin = mp.Leverage > 0,
        AccountEquityAtOpen = mp.AccountEquityAtOpen
    };

    private sealed class MutablePosition
    {
        public long Ticket;
        public string Symbol = string.Empty;
        public OrderType Type;
        public double Volume, OpenPrice, ClosePrice, SL, TP, Commission, Swap, Profit, ReturnPct;
        public long OpenTime, CloseTime;
        public string Comment = string.Empty;
        public double Leverage;
        public double AccountEquityAtOpen;
    }

    private sealed class QueuedMarketOrder
    {
        public long ExecutionTime;
        public string Symbol = string.Empty;
        public OrderType Type;
        public double Volume, SL, TP;
        public string Comment = string.Empty;
    }
}
