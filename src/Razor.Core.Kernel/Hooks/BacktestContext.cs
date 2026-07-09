using Razor.Core.Kernel.Clock;
using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;

namespace Razor.Core.Kernel.Hooks;

/// <summary>
/// Context provided to backtest hook callbacks.
/// </summary>
public sealed class BacktestContext : IBacktestContext
{
    /// <inheritdoc/>
    public Tick CurrentTick { get; }

    /// <inheritdoc/>
    public int TickIndex { get; }

    /// <inheritdoc/>
    public long TotalTicks { get; }

    /// <inheritdoc/>
    public double CurrentEquity { get; }

    /// <inheritdoc/>
    public double CurrentBalance { get; }

    /// <inheritdoc/>
    public double CurrentDrawdown { get; }

    /// <inheritdoc/>
    public IBroker Broker { get; }

    /// <inheritdoc/>
    public TickWindow TickWindow { get; }

    /// <inheritdoc/>
    public IReadOnlyList<Position> OpenPositions { get; }

    /// <inheritdoc/>
    public string HookName { get; }

    /// <inheritdoc/>
    public DateTime UtcNow { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new backtest context.
    /// </summary>
    public BacktestContext(
        TickClock clock,
        Tick currentTick,
        long tickIndex,
        long totalTicks,
        double equity,
        double balance,
        double drawdown,
        IBroker broker,
        TickWindow tickWindow,
        IReadOnlyList<Position> openPositions,
        string hookName = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clock);
        CurrentTick = currentTick;
        TickIndex = (int)tickIndex;
        TotalTicks = totalTicks;
        CurrentEquity = equity;
        CurrentBalance = balance;
        CurrentDrawdown = drawdown;
        Broker = broker;
        TickWindow = tickWindow;
        OpenPositions = openPositions;
        HookName = hookName;
        UtcNow = clock.GetUtcNow();
        CancellationToken = cancellationToken;
    }
}
