namespace Razor.Core.Sdk.Hooks;

/// <summary>
/// Root hook registry. Exposes typed sub‑registries for backtest, live, optimization,
/// and report pipelines. Plugin assemblies receive this via <see cref="IHookManifest.RegisterHooks"/>.
/// </summary>
public interface IHookRegistry
{
    /// <summary>Hooks for the backtesting pipeline.</summary>
    IBacktestHooks Backtest { get; }

    /// <summary>Hooks for the live trading pipeline.</summary>
    ILiveHooks Live { get; }

    /// <summary>Hooks for the optimization pipeline.</summary>
    IOptimizationHooks Optimization { get; }

    /// <summary>Hooks for the report generation pipeline.</summary>
    IReportHooks Report { get; }

    /// <summary>
    /// Clears all registered callbacks from all hook points across all pipelines.
    /// Called during extension reload to prevent accumulation of old callbacks.
    /// </summary>
    void ClearAll();
}
