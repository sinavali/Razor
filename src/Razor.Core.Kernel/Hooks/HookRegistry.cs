using Razor.Core.Sdk.Hooks;

namespace Razor.Core.Kernel.Hooks;

/// <summary>
/// Root hook registry exposing sub‑registries for backtest, live, optimization, and report pipelines.
/// Used by hook plugins to register callbacks.
/// </summary>
public sealed class HookRegistry : IHookRegistry
{
    /// <inheritdoc/>
    public IBacktestHooks Backtest { get; }

    /// <inheritdoc/>
    public ILiveHooks Live { get; }

    /// <inheritdoc/>
    public IOptimizationHooks Optimization { get; }

    /// <inheritdoc/>
    public IReportHooks Report { get; }

    /// <summary>
    /// Creates a new hook registry with empty sub‑registries.
    /// </summary>
    public HookRegistry()
    {
        Backtest = new BacktestHooks();
        Live = new LiveHooks();
        Optimization = new OptimizationHooks();
        Report = new ReportHooks();
    }

    /// <summary>
    /// Clears all registered callbacks from all hook points across all pipelines.
    /// </summary>
    public void ClearAll()
    {
        ((BacktestHooks)Backtest).ClearAll();
        ((LiveHooks)Live).ClearAll();
        ((OptimizationHooks)Optimization).ClearAll();
        ((ReportHooks)Report).ClearAll();
    }
}
