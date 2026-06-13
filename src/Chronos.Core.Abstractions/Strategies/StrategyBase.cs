using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Strategies;

/// <summary>
/// Convenience base class for strategies. Provides access to broker, tick window,
/// indicators, configuration, and async trading helper methods.
/// </summary>
public abstract class StrategyBase : IStrategy, IDisposable
{
    private readonly ReaderWriterLockSlim _geneLock = new(LockRecursionPolicy.SupportsRecursion);

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

    /// <summary>Optional neural network, if the strategy uses one.</summary>
    protected FeedForwardNetwork? NeuralNet { get; set; }

    /// <inheritdoc/>
    public virtual Task OnConfigureAsync(StrategySpecification spec) { Spec = spec; return Task.CompletedTask; }

    /// <inheritdoc/>
    public virtual Task OnStartAsync(IIndicatorRegistry indicators) { Indicators = indicators; return Task.CompletedTask; }

    /// <inheritdoc/>
    /// <remarks>
    /// The default implementation does nothing. Override this method to add your tick‑processing logic.
    /// For backward compatibility, the deprecated <see cref="OnTickAsync"/> will call this method.
    /// </remarks>
    public virtual void OnTick(Tick tick) { }

    /// <inheritdoc/>
    /// <remarks>
    /// <para><b>[Obsolete]</b> Override the synchronous <see cref="OnTick"/> instead.
    /// This method is retained for backward compatibility and will be removed in v2.0.</para>
    /// <para>The default implementation calls <see cref="OnTick"/> synchronously.</para>
    /// </remarks>
    [Obsolete("Use OnTick instead. This method will be removed in v2.0.", error: false)]
    public virtual Task OnTickAsync(Tick tick)
    {
        OnTick(tick);
        return Task.CompletedTask;
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
        try { GeneInjector.InjectAll(this, NeuralNet, genes); }
        finally { _geneLock.ExitWriteLock(); }
    }

    internal void WireUp(IBroker broker, TickWindow tickWindow)
    {
        Broker = broker;
        TickWindow = tickWindow;
    }

    /// <summary>Buys the primary symbol at market.</summary>
    protected Task<AdapterOrderResponse> BuyAsync(double volume, double? sl = null, double? tp = null, string? comment = null)
        => Broker.ExecuteMarketOrderAsync(PrimarySymbol, OrderType.Buy, volume, sl ?? 0, tp ?? 0, comment ?? "");

    /// <summary>Buys the given symbol at market.</summary>
    protected Task<AdapterOrderResponse> BuyAsync(string symbol, double volume, double? sl = null, double? tp = null, string? comment = null)
        => Broker.ExecuteMarketOrderAsync(symbol, OrderType.Buy, volume, sl ?? 0, tp ?? 0, comment ?? "");

    /// <summary>Sells the primary symbol at market.</summary>
    protected Task<AdapterOrderResponse> SellAsync(double volume, double? sl = null, double? tp = null, string? comment = null)
        => Broker.ExecuteMarketOrderAsync(PrimarySymbol, OrderType.Sell, volume, sl ?? 0, tp ?? 0, comment ?? "");

    /// <summary>Sells the given symbol at market.</summary>
    protected Task<AdapterOrderResponse> SellAsync(string symbol, double volume, double? sl = null, double? tp = null, string? comment = null)
        => Broker.ExecuteMarketOrderAsync(symbol, OrderType.Sell, volume, sl ?? 0, tp ?? 0, comment ?? "");

    /// <summary>Modifies an existing order's stop loss, take profit, or price.</summary>
    protected Task<AdapterOrderResponse> ModifyOrderAsync(long ticket, double? sl = null, double? tp = null, double? price = null)
        => Broker.ModifyOrderAsync(ticket, sl, tp, price);

    /// <summary>Cancels a pending order by ticket.</summary>
    protected Task<AdapterOrderResponse> CancelOrderAsync(long ticket) => Broker.CancelOrderAsync(ticket);

    /// <summary>Closes all positions for the primary symbol.</summary>
    protected Task CloseAllAsync(OrderType? type = null) => Broker.CloseAllAsync(PrimarySymbol, type);

    /// <summary>Closes all positions for the given symbol.</summary>
    protected Task CloseAllAsync(string symbol, OrderType? type = null) => Broker.CloseAllAsync(symbol, type);

    /// <inheritdoc/>
    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }

    /// <summary>Releases the lock resource.</summary>
    protected virtual void Dispose(bool disposing) {
        if (disposing)
        {
            _geneLock.Dispose();
        }
    }
}
