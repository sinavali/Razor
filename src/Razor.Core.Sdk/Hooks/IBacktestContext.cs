using Razor.Core.Sdk.Shared;

namespace Razor.Core.Sdk.Hooks;

/// <summary>
/// Context provided to backtest hook callbacks.
/// </summary>
public interface IBacktestContext : IHookContext
{
    /// <summary>The current tick being processed.</summary>
    Tick CurrentTick { get; }

    /// <summary>Zero‑based index of the current tick in the merged stream.</summary>
    int TickIndex { get; }

    /// <summary>Total number of ticks to be processed.</summary>
    long TotalTicks { get; }

    /// <summary>Current account equity.</summary>
    double CurrentEquity { get; }

    /// <summary>Current account balance.</summary>
    double CurrentBalance { get; }

    /// <summary>Current maximum drawdown percentage.</summary>
    double CurrentDrawdown { get; }

    /// <summary>The broker for the current backtest.</summary>
    IBroker Broker { get; }

    /// <summary>The tick window for the current backtest.</summary>
    TickWindow TickWindow { get; }

    /// <summary>All currently open positions.</summary>
    IReadOnlyList<Position> OpenPositions { get; }
}
