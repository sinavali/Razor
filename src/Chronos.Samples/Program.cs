using System.Collections.Immutable;
using Chronos.Abstractions.Adapters;
using Chronos.Abstractions.Shared;
using Chronos.Abstractions.Strategies;
using Chronos.Kernel.Backtesting;
using Chronos.Kernel.Configuration;
using Chronos.Kernel.Indicators;
using Chronos.Kernel.Messaging;
using Chronos.Kernel.Metrics;
using Chronos.Kernel.Optimization;
using Chronos.Samples.Plugins.Adapters.MT5;
using Chronos.Samples.Plugins.Strategies.CfdSmtDivergence;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1303 // Do not pass literals as localized parameters – sample console app

namespace Chronos.Samples;

/// <summary>
///
/// </summary>
internal sealed class ZeroFriction : ISimulationFriction
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="type"></param>
    /// <param name="volume"></param>
    /// <param name="currentPrice"></param>
    /// <returns></returns>
    public double CalculateSlippage(string symbol, OrderType type, double volume, double currentPrice)
        => 0;
    /// <summary>
    ///
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="volume"></param>
    /// <returns></returns>
    public double CalculateCommission(string symbol, double volume)
        => 0;
}

internal static class Program
{
    private static async Task Main(string[] args)
    {
        // ---------- Configuration ----------
        const string mt5Host = "127.0.0.1";
        const int mt5Port = 5555;

        string[] symbols = { "EURUSD", "GBPUSD", "USDJPY" };
        var timeframe = TimeFrame.M5;
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

        double initialBalance = 10_000;
        double leverage = 30;

        // GA settings
        int masterSeed = 42;
        int generations = 10;
        int populationSize = 10;
        double mutationRate = 0.15;
        double crossoverRate = 0.7;
        double elitismPct = 0.1;
        int tournamentSize = 3;
        int parallelism = Environment.ProcessorCount;

        // ---------- 1. Setup MT5 adapter & fetch data ----------
        var adapterConfig = new Mt5AdapterConfiguration
        {
            BridgeHost = mt5Host,
            BridgePort = mt5Port,
            HistoryCachePath = Path.Combine(AppContext.BaseDirectory, "Mt5Cache"),
            ReconnectIntervalMs = 3000,
            MaxReconnectAttempts = 10,
            TimeoutMs = 60000
        };

        Mt5Adapter? adapter = null;
        BorrowedTickData? tickData = null;

        try
        {
#pragma warning disable CA2000 // Disposed in finally block
            adapter = new Mt5Adapter(adapterConfig);
#pragma warning restore CA2000

            Console.WriteLine("Connecting to MT5...");
            if (!await adapter.ConnectAsync(CancellationToken.None).ConfigureAwait(false))
            {
                Console.WriteLine("Connection failed. Exiting.");
                return;
            }

            Console.WriteLine("Connected.");

            // Fetch historical data for all symbols
            var fetchTasks = symbols.Select(async sym =>
            {
                var req = new HistoricalDataRequest
                {
                    Symbol = sym,
                    StartTime = startDate,
                    EndTime = endDate,
                    RetentionPolicy = DataActionPolicy.PersistentCache
                };
                return await adapter.FetchHistoryToBinaryFileAsync(req, CancellationToken.None)
                    .ConfigureAwait(false);
            });
            var responses = await Task.WhenAll(fetchTasks).ConfigureAwait(false);

            // Map binary files to tick streams
            var mappedLists = new List<MemoryMappedTickList>();
            var filePaths = new List<string>();
            var streams = new IReadOnlyList<Tick>[symbols.Length];
            for (int i = 0; i < symbols.Length; i++)
            {
                var resp = responses[i];
                if (!resp.Success)
                {
                    Console.WriteLine($"Failed to fetch {symbols[i]}: {resp.ErrorMessage}");
                    return;
                }

                var mmList = BinaryDataMapper.MapTicks(resp.BinaryFilePath);
                mappedLists.Add(mmList);
                filePaths.Add(resp.BinaryFilePath);
                streams[i] = mmList;
                Console.WriteLine($"Fetched {resp.TotalRecords} ticks for {resp.Symbol}");
            }

            // Wrap in BorrowedTickData (will be disposed in finally)
#pragma warning disable CA2000 // Disposed in finally block
            tickData = new BorrowedTickData(streams, symbols, mappedLists, filePaths, adapter);
#pragma warning restore CA2000

            // ---------- 2. Build specifications ----------
            var symbolRequests = symbols
                .Select(s => new SymbolRequest(s, ImmutableArray.Create(timeframe)))
                .ToImmutableArray();

            var fitnessModel = new NetProfitFitness();

            var strategySpec = StrategySpecification.CreateValidated(initialBalance, leverage, symbolRequests, fitnessModel: fitnessModel);

            strategySpec = strategySpec with { FrictionModel = new ZeroFriction() };

            var execSpec = ExecutionSpecification.CreateValidated(
                startDate, endDate,
                warmupBars: 5,
                maxOpenPositions: 3,
                stopOutLevel: 0.5,
                latencyTicks: 0,
                geneInitializationSeed: masterSeed,
                maxParallelThreads: parallelism);

            var optSpec = OptimizationSpecification.CreateValidated(
                masterSeed, generations, populationSize,
                windows: 1,
                trainSplit: 0.8,
                mutationRate, crossoverRate,
                elitismPct, tournamentSize,
                fitnessModel);

            var symbolProperties = new Dictionary<string, SymbolProperties>
            {
                ["EURUSD"] = new()
                    { AssetClass = AssetClass.CFD, TickSize = 0.00001, ContractSize = 100_000, MinVolume = 0.01 },
                ["GBPUSD"] = new()
                    { AssetClass = AssetClass.CFD, TickSize = 0.00001, ContractSize = 100_000, MinVolume = 0.01 },
                ["USDJPY"] = new()
                    { AssetClass = AssetClass.CFD, TickSize = 0.001, ContractSize = 100_000, MinVolume = 0.01 }
            };

            // ---------- 3. Build gene schema ----------
            var neuralTopology = new[] { 5, 5, 1 };
            var schema = GeneInjector.BuildCompleteSchema(
                typeof(CfdSmtDivergenceStrategy), neuralTopology, ActivationFunction.Tanh);
            Console.WriteLine($"Total genes: {schema.Count}");

            // ---------- 4. Genetic optimiser ----------
            var optimiser = new GeneticOptimizer(
                schema,
                populationSize,
                masterSeed,
                mutationRate,
                crossoverRate,
                elitismPct,
                tournamentSize,
                stagnationGenerationsBeforeHyper: 3,
                maxDegreeOfParallelism: parallelism);

            // ---------- 5. Fitness evaluator ----------
            Func<Chromosome, CancellationToken, Task<double>> evaluator =
                async (chromosome, ct) =>
                {
                    var strategy = new CfdSmtDivergenceStrategy();
                    strategy.SetSymbolProperties(symbolProperties);
                    var calculator = new Mt5MarketCalculator();

                    var input = new BacktestInput
                    {
                        TickStreams = streams,
                        Symbols = symbols,
                        Strategy = strategy,
                        StrategySpecification = strategySpec,
                        ExecutionSpecification = execSpec,
                        MarketCalculator = calculator,
                        SymbolProperties = symbolProperties,
                        Genes = chromosome.Genes,
                        NeuralNetwork = null,
                        GeneInitializationSeed = execSpec.GeneInitializationSeed,
                        MessageBus = new MessageBus()
                    };

                    var runner = new BacktestRunner();
                    var result = await runner.RunAsync(input, ct).ConfigureAwait(false);
                    Console.WriteLine($"Trades: {result.TotalTrades}");
                    return FitnessCalculator.Calculate(result, fitnessModel, initialBalance);
                };

            // ---------- 6. Run the GA ----------
            Console.WriteLine("Initializing population...");
            optimiser.Initialize();

            for (int gen = 0; gen < generations; gen++)
            {
                Console.Write($"Gen {gen + 1}/{generations}: evaluating... ");
                await optimiser.EvaluateAsync(evaluator, CancellationToken.None).ConfigureAwait(false);
                var best = optimiser.BestSolution;
                Console.WriteLine($"Best fitness = {best.Fitness:F2} (hyper={optimiser.IsHyperMutation})");
                if (gen < generations - 1)
                    optimiser.Evolve();
            }

            // ---------- 7. Print best chromosome ----------
            var finalBest = optimiser.BestSolution;
            Console.WriteLine($"\nOptimisation complete. Best fitness: {finalBest.Fitness:F2}");
            Console.WriteLine("Best genes:");
            for (int i = 0; i < finalBest.Genes.Length; i++)
            {
                string name = i < schema.Count ? schema[i].Name : "NN_W" + (i - schema.Count);
                Console.WriteLine($"  {name}: {finalBest.Genes[i]:F4}");
            }
        }
        finally
        {
            // Cleanup – adapter.DisconnectAsync fully cleans up the bridge.
            if (tickData is not null)
                await tickData.DisposeAsync().ConfigureAwait(false);

            if (adapter is not null)
                await adapter.DisconnectAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Simple fitness model that optimises net profit.
    /// </summary>
    private sealed class NetProfitFitness : IFitnessModel
    {
        public double Evaluate(double finalBalance, double initialBalance, double maxDrawdown,
            double maxDailyDrawdown, int totalTrades, IReadOnlyList<Position> history)
        {
            if (totalTrades == 0)
                return -1_000_000;   // huge penalty for no trades
            return finalBalance - initialBalance;
        }
    }
}
