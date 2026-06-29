using Chronos.Core.Abstractions.Slots;

namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Convenience base class for strategies. Provides access to broker, tick window,
/// indicators, configuration, and async trading helper methods.
/// </summary>
public abstract class StrategyBase : IStrategyCapability, IDisposable
{
    private readonly ReaderWriterLockSlim _geneLock = new(LockRecursionPolicy.SupportsRecursion);
    private IBehaviorRecorder? _behaviorRecorder;

    /// <summary>Lock used to protect gene injection while the strategy is processing ticks.</summary>
    public ReaderWriterLockSlim GeneLock => _geneLock;

    /// <summary>The broker (simulated or live).</summary>
    protected IBroker Broker { get; private set; } = null!;

    /// <summary>Sliding tick window for accessing recent ticks and OHLC statistics.</summary>
    protected TickWindow TickWindow { get; private set; } = null!;

    /// <summary>The indicator registry.</summary>
    protected IIndicatorRegistry Indicators { get; private set; } = null!;

    /// <summary>The current strategy specification (immutable).</summary>
    protected StrategySpecification Spec { get; private set; } = null!;

    /// <summary>Primary symbol shortcut (first requested symbol).</summary>
    protected string PrimarySymbol =>
        Spec.RequestedSymbols.Length > 0 ? Spec.RequestedSymbols[0].Symbol : string.Empty;

    /// <summary>Optional neural network model, if the strategy uses one.</summary>
    public INeuralNetworkModel? NeuralNetwork { get; set; }

    /// <summary>Whether behavior logging is currently enabled.</summary>
    public bool IsBehaviorLoggingEnabled { get; internal set; }

    /// <inheritdoc/>
    public virtual int TotalGeneCount
    {
        get
        {
            int count = GeneInjector.GetGeneProperties(GetType()).Count;
            if (NeuralNetwork != null)
            {
                count += NeuralNetwork.ParameterCount;
            }

            return count;
        }
    }

    /// <inheritdoc/>
    public virtual bool RequiresNeuralNetwork => false;

    /// <inheritdoc/>
    public virtual Task OnConfigureAsync(StrategySpecification spec)
    {
        Spec = spec;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task OnStartAsync(IIndicatorRegistry indicators)
    {
        Indicators = indicators;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The default implementation does nothing. Override this method to add your tick‑processing logic.
    /// </remarks>
    public virtual void OnTick(string symbol, Tick tick)
    {
    }

    /// <inheritdoc/>
    public virtual Task OnStopAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when a tracked timeframe window completes.
    /// Override to receive bar‑style notifications.
    /// </summary>
    protected virtual Task OnWindowCompletedAsync(string symbol, TimeFrame tf)
        => Task.CompletedTask;

    /// <summary>
    /// Called by the engine when a window completes. Forwards to the protected virtual
    /// <see cref="OnWindowCompletedAsync"/> so that derived strategies can override it.
    /// </summary>
    internal async Task NotifyWindowCompletedAsync(string symbol, TimeFrame tf)
    {
        await OnWindowCompletedAsync(symbol, tf).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual void InjectGenes(double[] genes)
    {
        _geneLock.EnterWriteLock();
        try
        {
            GeneInjector.InjectAll(this, NeuralNetwork, genes);
        }
        finally
        {
            _geneLock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public virtual double[] ExportGenes()
    {
        _geneLock.EnterReadLock();
        try
        {
            var propertyGenes = GeneInjector.ExtractGenes(this);
            if (NeuralNetwork == null)
            {
                return propertyGenes;
            }

            return [.. propertyGenes, .. NeuralNetwork.ExportParameters()];
        }
        finally
        {
            _geneLock.ExitReadLock();
        }
    }

    internal void WireUp(IBroker broker, TickWindow tickWindow)
    {
        Broker = broker;
        TickWindow = tickWindow;
    }

    // ─── Trade Helpers (overridden to automatically record) ──────────

    /// <summary>Buys the primary symbol at market.</summary>
    protected Task<AdapterOrderResponse> BuyAsync(double volume, double? sl = null, double? tp = null,
        string? comment = null)
    {
        var task = Broker.ExecuteMarketOrderAsync(PrimarySymbol, OrderType.Buy, volume, sl ?? 0, tp ?? 0, comment ?? "");
        return task;
    }

    /// <summary>Buys the given symbol at market.</summary>
    protected Task<AdapterOrderResponse> BuyAsync(string symbol, double volume, double? sl = null, double? tp = null,
        string? comment = null)
    {
        var task = Broker.ExecuteMarketOrderAsync(symbol, OrderType.Buy, volume, sl ?? 0, tp ?? 0, comment ?? "");
        return task;
    }

    /// <summary>Sells the primary symbol at market.</summary>
    protected Task<AdapterOrderResponse> SellAsync(double volume, double? sl = null, double? tp = null,
        string? comment = null)
    {
        var task = Broker.ExecuteMarketOrderAsync(PrimarySymbol, OrderType.Sell, volume, sl ?? 0, tp ?? 0, comment ?? "");
        return task;
    }

    /// <summary>Sells the given symbol at market.</summary>
    protected Task<AdapterOrderResponse> SellAsync(string symbol, double volume, double? sl = null, double? tp = null,
        string? comment = null)
    {
        var task = Broker.ExecuteMarketOrderAsync(symbol, OrderType.Sell, volume, sl ?? 0, tp ?? 0, comment ?? "");
        return task;
    }

    /// <summary>Modifies an existing order's stop loss, take profit, or price.</summary>
    protected Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null,
        double? price = null)
    {
        var task = Broker.ModifyOrderAsync(ticket, sl, tp, price);
        return task;
    }

    /// <summary>Cancels a pending order by ticket.</summary>
    protected Task<AdapterOrderResponse> CancelOrderAsync(long ticket)
    {
        var task = Broker.CancelOrderAsync(ticket);
        return task;
    }

    /// <summary>Closes all positions for the primary symbol.</summary>
    protected Task CloseAllAsync(OrderType? type = null)
    {
        var task = Broker.CloseAllAsync(PrimarySymbol, type);
        return task;
    }

    /// <summary>Closes all positions for the given symbol.</summary>
    protected Task CloseAllAsync(string symbol, OrderType? type = null)
    {
        var task = Broker.CloseAllAsync(symbol, type);
        return task;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the lock resource.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _geneLock.Dispose();
        }
    }

    // Placeholder for session ID (set by engine)
    internal string? SessionId { get; set; }
}
