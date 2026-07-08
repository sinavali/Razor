using Chronos.Core.Kernel.Clock;
using Chronos.Core.Sdk.Hooks;
using Chronos.Core.Sdk.Shared;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Context provided to live trading hook callbacks.
/// </summary>
public sealed class LiveContext : ILiveContext
{
    /// <inheritdoc/>
    public Tick CurrentTick { get; }

    /// <inheritdoc/>
    public double CurrentEquity { get; }

    /// <inheritdoc/>
    public double CurrentBalance { get; }

    /// <inheritdoc/>
    public double CurrentDrawdown { get; }

    /// <inheritdoc/>
    public IBroker Broker { get; }

    /// <inheritdoc/>
    public string AdapterName { get; }

    /// <inheritdoc/>
    public bool IsConnected { get; }

    /// <inheritdoc/>
    public string HookName { get; }

    /// <inheritdoc/>
    public DateTime UtcNow { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new live context.
    /// </summary>
    public LiveContext(
        SystemClock clock,
        Tick currentTick,
        double equity,
        double balance,
        double drawdown,
        IBroker broker,
        string adapterName,
        bool isConnected,
        string hookName = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clock);
        CurrentTick = currentTick;
        CurrentEquity = equity;
        CurrentBalance = balance;
        CurrentDrawdown = drawdown;
        Broker = broker;
        AdapterName = adapterName;
        IsConnected = isConnected;
        HookName = hookName;
        UtcNow = clock.GetUtcNow();
        CancellationToken = cancellationToken;
    }
}
