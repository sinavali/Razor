using Chronos.Core.Adapters;
using Chronos.Core.Configuration;
using Chronos.Core.Fitness;
using Chronos.Core.Friction;
using Chronos.Core.Notifications;
using Chronos.Core.Trading;
using Chronos.Core.Utils;
using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Chronos.Orchestration.Configuration;

/// <summary>
/// Responsible for creating immutable specification records from application configuration sources.
/// </summary>
public static class ConfigurationLoader
{
    private static readonly Action<ILogger, string, object, Exception?> DefaultedConfigWarning =
        LoggerMessage.Define<string, object>(LogLevel.Warning, new EventId(1, nameof(DefaultedConfigWarning)),
            "{Parameter} not configured, defaulting to {Default}");

    // ── Strategy ──

    /// <summary>Loads a <see cref="StrategySpecification"/> from the provided <see cref="IConfiguration"/>.</summary>
    public static StrategySpecification LoadStrategySpecification(IConfiguration configuration,
        ISimulationFriction? friction = null, IFitnessModel? fitness = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetSection("InitialBalance").Exists())
            DefaultedConfigWarningIfNotNull(logger, "InitialBalance", 10000);
        double initialBalance = configuration.GetValue<double>("InitialBalance", 10000);

        if (!configuration.GetSection("Leverage").Exists())
            throw new ConfigurationException("Leverage is required and must be configured.");
        double leverage = configuration.GetValue<double>("Leverage");

        var spec = new StrategySpecification
        {
            InitialBalance = initialBalance,
            Leverage = leverage,
            FrictionModel = friction,
            FitnessModel = fitness,
            RequestedSymbols = LoadSymbolRequests(configuration.GetSection("Symbols"))
        };
        spec.Validate();
        return spec;
    }

    // ── Execution ──

    /// <summary>Loads an <see cref="ExecutionSpecification"/> from the provided <see cref="IConfiguration"/>.</summary>
    public static ExecutionSpecification LoadExecutionSpecification(IConfiguration configuration,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetSection("StartDate").Exists())
            DefaultedConfigWarningIfNotNull(logger, "StartDate", "one year ago");
        DateTime startDate = configuration.GetValue<DateTime>("StartDate", DateTime.UtcNow.AddYears(-1));

        if (!configuration.GetSection("EndDate").Exists())
            DefaultedConfigWarningIfNotNull(logger, "EndDate", "now");
        DateTime endDate = configuration.GetValue<DateTime>("EndDate", DateTime.UtcNow);

        // MaxParallelThreads is a non‑critical parameter; safe to default to 0 (auto).
        if (!configuration.GetSection("MaxParallelThreads").Exists())
            DefaultedConfigWarningIfNotNull(logger, "MaxParallelThreads", 0);
        int maxParallelThreads = configuration.GetValue<int>("MaxParallelThreads", 0);

        // LatencyTicks: required – affects trading behaviour.
        if (!configuration.GetSection("LatencyTicks").Exists())
            throw new ConfigurationException("LatencyTicks is required and must be configured.");
        long latencyTicks = configuration.GetValue<long>("LatencyTicks");

        // WarmupWindowCount: required – affects trading behaviour.
        if (!configuration.GetSection("WarmupWindowCount").Exists() && !configuration.GetSection("WarmupBars").Exists())
            throw new ConfigurationException("WarmupWindowCount is required and must be configured.");
        int warmupWindowCount = configuration.GetValue<int>("WarmupWindowCount",
            configuration.GetValue<int>("WarmupBars"));

        // MaxOpenPositions: required – affects trading behaviour.
        if (!configuration.GetSection("MaxOpenPositions").Exists())
            throw new ConfigurationException("MaxOpenPositions is required and must be configured.");
        int maxOpenPositions = configuration.GetValue<int>("MaxOpenPositions");

        // StopOutLevel: required – critical risk parameter (Principle 9).
        if (!configuration.GetSection("StopOutLevel").Exists())
            throw new ConfigurationException("StopOutLevel is required and must be configured.");
        double stopOutLevel = configuration.GetValue<double>("StopOutLevel");

        // HistoricalDataPolicy: non‑critical, safe to default.
        if (!configuration.GetSection("HistoricalDataPolicy").Exists())
            DefaultedConfigWarningIfNotNull(logger, "HistoricalDataPolicy", DataActionPolicy.DeleteAfterTask);
        DataActionPolicy historicalDataPolicy =
            configuration.GetValue<DataActionPolicy>("HistoricalDataPolicy", DataActionPolicy.DeleteAfterTask);

        // GeneInitializationSeed: required – affects determinism.
        if (!configuration.GetSection("GeneInitializationSeed").Exists())
            throw new ConfigurationException("GeneInitializationSeed is required and must be configured.");
        int geneInitializationSeed = configuration.GetValue<int>("GeneInitializationSeed");

        var spec = new ExecutionSpecification
        {
            StartDate = startDate,
            EndDate = endDate,
            MaxParallelThreads = maxParallelThreads,
            LatencyTicks = latencyTicks,
            WarmupWindowCount = warmupWindowCount,
            MaxOpenPositions = maxOpenPositions,
            StopOutLevel = stopOutLevel,
            HistoricalDataPolicy = historicalDataPolicy,
            GeneInitializationSeed = geneInitializationSeed
        };
        spec.Validate();
        return spec;
    }

    // ── Optimisation ──

    /// <summary>Loads an <see cref="OptimizationSpecification"/> from the provided <see cref="IConfiguration"/>.</summary>
    public static OptimizationSpecification LoadOptimizationSpecification(IConfiguration configuration,
        IFitnessModel? fitnessModel = null, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetSection("MasterSeed").Exists())
            DefaultedConfigWarningIfNotNull(logger, "MasterSeed", 12345);
        int masterSeed = configuration.GetValue<int>("MasterSeed", 12345);

        if (!configuration.GetSection("Generations").Exists())
            DefaultedConfigWarningIfNotNull(logger, "Generations", 10);
        int generations = configuration.GetValue<int>("Generations", 10);

        if (!configuration.GetSection("PopulationSize").Exists())
            DefaultedConfigWarningIfNotNull(logger, "PopulationSize", 100);
        int populationSize = configuration.GetValue<int>("PopulationSize", 100);

        if (!configuration.GetSection("Windows").Exists())
            DefaultedConfigWarningIfNotNull(logger, "Windows", 1);
        int windows = configuration.GetValue<int>("Windows", 1);

        if (!configuration.GetSection("TrainSplit").Exists())
            DefaultedConfigWarningIfNotNull(logger, "TrainSplit", 0.7);
        double trainSplit = configuration.GetValue<double>("TrainSplit", 0.7);

        if (!configuration.GetSection("MutationRate").Exists())
            DefaultedConfigWarningIfNotNull(logger, "MutationRate", 0.1);
        double mutationRate = configuration.GetValue<double>("MutationRate", 0.1);

        if (!configuration.GetSection("CrossoverRate").Exists())
            DefaultedConfigWarningIfNotNull(logger, "CrossoverRate", 0.5);
        double crossoverRate = configuration.GetValue<double>("CrossoverRate", 0.5);

        if (!configuration.GetSection("ElitismPct").Exists())
            DefaultedConfigWarningIfNotNull(logger, "ElitismPct", 0.05);
        double elitismPct = configuration.GetValue<double>("ElitismPct", 0.05);

        if (!configuration.GetSection("TournamentSize").Exists())
            DefaultedConfigWarningIfNotNull(logger, "TournamentSize", 3);
        int tournamentSize = configuration.GetValue<int>("TournamentSize", 3);

        if (!configuration.GetSection("StagnationGenerationsBeforeHyper").Exists())
            DefaultedConfigWarningIfNotNull(logger, "StagnationGenerationsBeforeHyper", 3);
        int stagnationGenerationsBeforeHyper = configuration.GetValue<int>("StagnationGenerationsBeforeHyper", 3);

        var spec = new OptimizationSpecification
        {
            MasterSeed = masterSeed,
            Generations = generations,
            PopulationSize = populationSize,
            Windows = windows,
            TrainSplit = trainSplit,
            MutationRate = mutationRate,
            CrossoverRate = crossoverRate,
            ElitismPct = elitismPct,
            TournamentSize = tournamentSize,
            StagnationGenerationsBeforeHyper = stagnationGenerationsBeforeHyper,
            FitnessModel = fitnessModel
        };
        spec.Validate();
        return spec;
    }

    // ── Live ──

    /// <summary>Loads a <see cref="LiveSpecification"/> from the provided <see cref="IConfiguration"/>.</summary>
    public static LiveSpecification LoadLiveSpecification(IConfiguration configuration,
        params INotificationChannel[] notificationChannels)
    {
        return LoadLiveSpecification(configuration, null, notificationChannels);
    }

    /// <summary>Loads a <see cref="LiveSpecification"/> with an optional logger.</summary>
    public static LiveSpecification LoadLiveSpecification(IConfiguration configuration,
        ILogger? logger, params INotificationChannel[] notificationChannels)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetSection("MagicNumber").Exists())
            DefaultedConfigWarningIfNotNull(logger, "MagicNumber", 1000);
        int magicNumber = configuration.GetValue<int>("MagicNumber", 1000);

        if (!configuration.GetSection("ContinuousOptimization").Exists())
            DefaultedConfigWarningIfNotNull(logger, "ContinuousOptimization", false);
        bool continuousOptimization = configuration.GetValue<bool>("ContinuousOptimization", false);

        if (!configuration.GetSection("LookbackDays").Exists())
            DefaultedConfigWarningIfNotNull(logger, "LookbackDays", 30);
        int lookbackDays = configuration.GetValue<int>("LookbackDays", 30);

        if (!configuration.GetSection("SkipRecentDays").Exists())
            DefaultedConfigWarningIfNotNull(logger, "SkipRecentDays", 0);
        int skipRecentDays = configuration.GetValue<int>("SkipRecentDays", 0);

        if (!configuration.GetSection("InitialDelayMinutes").Exists())
            DefaultedConfigWarningIfNotNull(logger, "InitialDelayMinutes", 1);
        int initialDelayMinutes = configuration.GetValue<int>("InitialDelayMinutes", 1);

        if (!configuration.GetSection("OptimizationIntervalHours").Exists())
            DefaultedConfigWarningIfNotNull(logger, "OptimizationIntervalHours", 24);
        int optimizationIntervalHours = configuration.GetValue<int>("OptimizationIntervalHours", 24);

        if (!configuration.GetSection("RotateOptimizationSeed").Exists())
            DefaultedConfigWarningIfNotNull(logger, "RotateOptimizationSeed", false);
        bool rotateOptimizationSeed = configuration.GetValue<bool>("RotateOptimizationSeed", false);

        if (!configuration.GetSection("OrderGuardTimeoutSeconds").Exists())
            DefaultedConfigWarningIfNotNull(logger, "OrderGuardTimeoutSeconds", 5);
        int orderGuardTimeoutSeconds = configuration.GetValue<int>("OrderGuardTimeoutSeconds", 5);

        var spec = new LiveSpecification
        {
            MagicNumber = magicNumber,
            ContinuousOptimization = continuousOptimization,
            LookbackDays = lookbackDays,
            SkipRecentDays = skipRecentDays,
            InitialDelayMinutes = initialDelayMinutes,
            OptimizationIntervalHours = optimizationIntervalHours,
            RotateOptimizationSeed = rotateOptimizationSeed,
            OrderGuardTimeoutSeconds = orderGuardTimeoutSeconds,
            NotificationChannels = [.. notificationChannels]
        };
        spec.Validate();
        return spec;
    }

    // ── Helpers ──

    private static void DefaultedConfigWarningIfNotNull(ILogger? logger, string parameter, object defaultValue)
    {
        if (logger is not null)
            DefaultedConfigWarning(logger, parameter, defaultValue, null);
    }

    private static ImmutableArray<SymbolRequest> LoadSymbolRequests(IConfigurationSection section)
    {
        var list = new List<SymbolRequest>();
        foreach (var child in section.GetChildren())
        {
            var symbol = child.GetValue<string>("Name") ?? child.Key;
            var timeframesRaw = child.GetValue<string>("TimeFrames") ?? "H1";
            var parsedTfs = new HashSet<TimeFrame>();

            foreach (var tf in timeframesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Enum.TryParse<TimeFrame>(tf.Trim(), ignoreCase: true, out var parsed))
                    throw new ConfigurationException(
                        $"Invalid TimeFrame '{tf.Trim()}' for symbol '{symbol}'. Valid values: {string.Join(", ", Enum.GetNames<TimeFrame>())}");

                if (!parsedTfs.Add(parsed))
                    throw new ConfigurationException($"Duplicate timeframe '{parsed}' for symbol '{symbol}'.");
            }

            list.Add(new SymbolRequest(symbol, [.. parsedTfs]));
        }

        return [.. list];
    }
}
