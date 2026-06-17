using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Backtesting;

/// <summary>Minimal input contract for backtest engines.</summary>
public interface IBacktestInput
{
    /// <summary>Pre‑loaded tick streams, one per symbol (may be memory‑mapped).</summary>
    IReadOnlyList<Tick>[] TickStreams { get; }

    /// <summary>Symbol names in the same order as <see cref="TickStreams"/>.</summary>
    string[] Symbols { get; }
}
