using Razor.Core.Kernel.Backtesting;
using Razor.Core.Kernel.Clock;
using Razor.Core.Kernel.Configuration;
using Razor.Core.Kernel.Hooks;
using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;
using Chromosome = Razor.Core.Kernel.Optimization.Chromosome;

namespace Razor.Core.Kernel.Reporting;

/// <summary>
/// Generates reports from backtest or optimisation results and invokes the report hooks.
/// </summary>
public sealed class ReportGenerator
{
    private readonly IReportHooks _hooks;
    private readonly SystemClock _clock;

    /// <summary>
    /// Initialises a new instance of the <see cref="ReportGenerator"/> class.
    /// </summary>
    /// <param name="hooks">The report hook registry.</param>
    /// <param name="clock">The system clock used for timestamps.</param>
    public ReportGenerator(IReportHooks hooks, SystemClock clock)
    {
        _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Generates a report from a backtest result.
    /// </summary>
    /// <param name="result">The completed backtest result.</param>
    /// <param name="spec">The strategy specification used for the run.</param>
    /// <param name="execSpec">The execution specification.</param>
    public void GenerateBacktestReport(BacktestResult result, StrategySpecification spec, ExecutionSpecification execSpec)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(execSpec);

        var request = new ReportRequest
        {
            ReportTitle = "Backtest Report",
            TradeHistory = result.History,
            InitialBalance = spec.InitialBalance,
            FinalBalance = result.Balance,
            MaxDrawdown = result.Drawdown,
            StartDate = execSpec.StartDate,
            EndDate = execSpec.EndDate,
            AdditionalMetrics = new Dictionary<string, double>
            {
                { "NetProfit", result.Balance - spec.InitialBalance },
                { "TotalTrades", result.TotalTrades },
                { "MaxDailyDrawdown", result.DailyDrawdown }
            }
        };

        GenerateReport(request);
    }

    /// <summary>
    /// Generates a report from an optimisation result (best chromosome and its fitness).
    /// </summary>
    /// <param name="best">The best chromosome found.</param>
    /// <param name="spec">The optimisation specification.</param>
    public void GenerateOptimizationReport(Chromosome best, OptimizationSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(best);
        ArgumentNullException.ThrowIfNull(spec);

        var request = new ReportRequest
        {
            ReportTitle = "Optimization Report",
            TradeHistory = Array.Empty<Position>(),
            InitialBalance = 0,
            FinalBalance = 0,
            MaxDrawdown = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow,
            AdditionalMetrics = new Dictionary<string, double>
            {
                { "BestFitness", best.Fitness },
                { "Generations", spec.Generations },
                { "PopulationSize", spec.PopulationSize }
            }
        };

        GenerateReport(request);
    }

    private void GenerateReport(ReportRequest request)
    {
        var context = new ReportContext(_clock, "JSON", "report.before_generate");
        var filterResult = _hooks.OnBeforeGenerate.InvokeFilterChain(request, context);
        if (!filterResult.IsAllowed)
        {
            // Report generation rejected
            return;
        }

        var finalRequest = filterResult.Data ?? request;

        // A hook plugin would generate the actual report (PDF, HTML, etc.).
        // We produce a dummy byte array to simulate generation.
        byte[] reportData = System.Text.Encoding.UTF8.GetBytes("Report generated.");

        var afterContext = new ReportContext(_clock, "JSON", "report.after_generate");
        _hooks.OnAfterGenerate.InvokeActionChain((reportData, "JSON"), afterContext);
    }
}
