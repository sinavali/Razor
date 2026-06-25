// -----------------------------------------------------------------------------
// <copyright file="KernelService.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Kernel;

using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;
using Chronos.Core.Abstractions.Slots;
using Chronos.Core.Engine.Extensions;
using Chronos.Core.Engine.Management.Tasks;
using Chronos.Core.Kernel.Backtesting;
using Chronos.Core.Kernel.Brokers;
using Chronos.Core.Kernel.Clock;
using Chronos.Core.Kernel.Configuration;
using Chronos.Core.Kernel.Indicators;
using Chronos.Core.Kernel.Messaging;
using Chronos.Core.Kernel.Optimization;
using Chronos.Core.Kernel.Telemetry;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using ChromosomeKernel = Chronos.Core.Kernel.Optimization.Chromosome;

/// <summary>
/// Real implementation of <see cref="IKernelService"/> using the Chronos.Core.Kernel components.
/// </summary>
internal sealed class KernelService : IKernelService, IDisposable
{
    private readonly IExtensionManager _extensionManager;
    private readonly IHookRegistry _hookRegistry;
    private readonly IMessageBus _messageBus;
    private readonly ICoreMetrics _metrics;
    private readonly ILogger<KernelService> _logger;
    private readonly ConcurrentDictionary<string, TaskState> _activeTasks = new();
    private readonly SemaphoreSlim _taskLock = new(1, 1);
    private bool _disposed;

    // Logger delegates
    private static readonly Action<ILogger, string, Exception?> _logBacktestStarted =
        LoggerMessage.Define<string>(LogLevel.Information, 0, "Backtest {TaskId} started.");
    private static readonly Action<ILogger, string, Exception?> _logBacktestCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, 1, "Backtest {TaskId} completed.");
    private static readonly Action<ILogger, string, Exception?> _logBacktestFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 2, "Backtest {TaskId} failed.");
    private static readonly Action<ILogger, string, Exception?> _logLiveStarted =
        LoggerMessage.Define<string>(LogLevel.Information, 3, "Live {TaskId} started.");
    private static readonly Action<ILogger, string, Exception?> _logLiveStopped =
        LoggerMessage.Define<string>(LogLevel.Information, 4, "Live {TaskId} stopped.");
    private static readonly Action<ILogger, string, Exception?> _logOptimizationStarted =
        LoggerMessage.Define<string>(LogLevel.Information, 5, "Optimization {TaskId} started.");
    private static readonly Action<ILogger, string, Exception?> _logOptimizationCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, 6, "Optimization {TaskId} completed.");
    private static readonly Action<ILogger, string, Exception?> _logBacktestCancelled =
        LoggerMessage.Define<string>(LogLevel.Information, 7, "Backtest {TaskId} cancelled.");
    private static readonly Action<ILogger, string, Exception?> _logOptimizationCancelled =
        LoggerMessage.Define<string>(LogLevel.Information, 8, "Optimization {TaskId} cancelled.");
    private static readonly Action<ILogger, string, Exception?> _logOptimizationFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 9, "Optimization {TaskId} failed.");
    private static readonly Action<ILogger, Exception?> _logPauseLiveNotImplemented =
        LoggerMessage.Define(LogLevel.Warning, 10, "PauseLive not implemented; ignoring.");
    private static readonly Action<ILogger, Exception?> _logResumeLiveNotImplemented =
        LoggerMessage.Define(LogLevel.Warning, 11, "ResumeLive not implemented; ignoring.");
    private static readonly Action<ILogger, string, string, Exception?> _logBacktestDataFetchFailed =
        LoggerMessage.Define<string, string>(LogLevel.Error, 12, "Failed to fetch historical data for symbol {Symbol} in backtest {TaskId}.");
    private static readonly Action<ILogger, string, Exception?> _logLiveTickHandlerError =
        LoggerMessage.Define<string>(LogLevel.Error, 13, "Error in live tick handler for symbol {Symbol}.");
    private static readonly Action<ILogger, string, Exception?> _logFailedUnsubscribe =
        LoggerMessage.Define<string>(LogLevel.Warning, 4, "Failed to unsubscribe from symbol {Symbol} during live stop.");

    private sealed record TaskState(
        CancellationTokenSource Cts,
        IAdapterCapability? Adapter = null,
        string[]? Symbols = null,
        Action<string, Tick>? TickHandler = null,
        object? Result = null,
        LiveBroker? Broker = null,
        IStrategyCapability? Strategy = null,
        OptimizationRunner? Runner = null,
        IIndicatorRegistry? IndicatorRegistry = null,
        List<MemoryMappedTickList>? MappedTickLists = null)
    {
        public void DisposeCts()
        {
            Cts?.Cancel();
            Cts?.Dispose();
        }

        public void DisposeIndicatorRegistry()
        {
            if (IndicatorRegistry == null)
            {
                return;
            }

            // Try to dispose via IDisposable if implemented
            if (IndicatorRegistry is IDisposable disposable)
            {
                disposable.Dispose();
                return;
            }

            // Fallback: call DisposeAll via reflection (IndicatorRegistry has this method)
            var method = IndicatorRegistry.GetType().GetMethod("DisposeAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(IndicatorRegistry, null);
            }
        }

        public void DisposeMappedTickLists()
        {
            if (MappedTickLists == null)
            {
                return;
            }

            foreach (var mm in MappedTickLists)
            {
                mm?.Dispose();
            }
            MappedTickLists.Clear();
        }
    }

    public KernelService(
        IExtensionManager extensionManager,
        IHookRegistry hookRegistry,
        IMessageBus messageBus,
        ICoreMetrics metrics,
        ILogger<KernelService> logger)
    {
        _extensionManager = extensionManager ?? throw new ArgumentNullException(nameof(extensionManager));
        _hookRegistry = hookRegistry ?? throw new ArgumentNullException(nameof(hookRegistry));
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string> StartBacktestAsync(
        IAdapterCapability adapter,
        IStrategyCapability strategy,
        BacktestConfiguration config,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(config);

        string taskId = $"bt_{Guid.NewGuid():N}";
        _logBacktestStarted(_logger, taskId, null);

        // CA2000 is suppressed because the CTS is stored in the task state and disposed
        // when the task completes or is cancelled.
        CancellationTokenSource cts;
#pragma warning disable CA2000 // Dispose objects before losing scope
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
#pragma warning restore CA2000
        var mappedLists = new List<MemoryMappedTickList>();
        var taskState = new TaskState(cts, MappedTickLists: mappedLists);

        try
        {
            _activeTasks[taskId] = taskState;
        }
        catch
        {
            cts.Dispose();
            throw;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // 1. Fetch historical data for each symbol
                var symbolProperties = new Dictionary<string, SymbolProperties>();
                var tickStreams = new List<IReadOnlyList<Tick>>();

                foreach (string symbol in config.Symbols)
                {
                    var props = await adapter.GetSymbolPropertiesAsync(symbol, cts.Token).ConfigureAwait(false);
                    if (props == null)
                    {
                        _logBacktestDataFetchFailed(_logger, symbol, taskId, null);
                        throw new InvalidOperationException($"Symbol properties for {symbol} not available.");
                    }
                    symbolProperties[symbol] = props;

                    // Fetch history
                    var request = new HistoricalDataRequest
                    {
                        Symbol = symbol,
                        StartTime = config.StartDate,
                        EndTime = config.EndDate,
                        RetentionPolicy = DataActionPolicy.DeleteAfterTask
                    };
                    var response = await adapter.FetchHistoryToBinaryFileAsync(request, cts.Token).ConfigureAwait(false);
                    if (!response.Success)
                    {
                        throw new InvalidOperationException($"Failed to fetch history for {symbol}: {response.ErrorMessage}");
                    }

                    // Map the binary file to a memory-mapped list
                    var mmList = new MemoryMappedTickList(response.BinaryFilePath);
                    mappedLists.Add(mmList);
                    tickStreams.Add(mmList);
                }

                // 2. Build StrategySpecification
                var timeframeEnums = config.Timeframes
                    .Select(ParseTimeFrame)
                    .Distinct()
                    .ToArray();

                var symbolRequests = config.Symbols.Select(s =>
                    new SymbolRequest(s, ImmutableArray.Create(timeframeEnums)))
                    .ToImmutableArray();

                var strategySpec = new StrategySpecification
                {
                    InitialBalance = config.InitialBalance,
                    Leverage = config.Leverage,
                    RequestedSymbols = symbolRequests
                };
                strategySpec.Validate();

                // 3. Build ExecutionSpecification
                var execSpec = new ExecutionSpecification
                {
                    StartDate = config.StartDate,
                    EndDate = config.EndDate,
                    MaxParallelThreads = config.MaxParallelThreads,
                    LatencyTicks = config.LatencyTicks,
                    WarmupWindowCount = config.WarmupWindowCount,
                    MaxOpenPositions = config.MaxOpenPositions,
                    StopOutLevel = config.StopOutLevel,
                    GeneInitializationSeed = config.GeneInitializationSeed
                };
                execSpec.Validate();

                // 4. Get neural network if requested
                INeuralNetworkModel? nnModel = null;
                if (!string.IsNullOrEmpty(config.NeuralNetworkName))
                {
                    nnModel = _extensionManager.ActiveNeuralNetwork;
                    if (nnModel == null)
                    {
                        throw new InvalidOperationException($"Neural network model '{config.NeuralNetworkName}' not active.");
                    }
                }

                // 5. Build BacktestInput
                var backtestInput = new BacktestInput
                {
                    TickStreams = tickStreams.ToArray(),
                    Symbols = config.Symbols,
                    Strategy = strategy,
                    StrategySpecification = strategySpec,
                    ExecutionSpecification = execSpec,
                    MarketCalculator = adapter.Calculator,
                    SymbolProperties = symbolProperties,
                    Genes = config.Genes.Length > 0 ? config.Genes : null,
                    GeneInitializationSeed = config.GeneInitializationSeed,
                    NeuralNetwork = nnModel,
                    Progress = null,
                    MessageBus = _messageBus,
                    Metrics = _metrics,
                    HookRegistry = _hookRegistry
                };

                // 6. Run backtest
                var runner = new BacktestRunner();
                var result = await runner.RunAsync(backtestInput, cts.Token).ConfigureAwait(false);

                // 7. Store result
                if (_activeTasks.TryGetValue(taskId, out var state))
                {
                    _activeTasks[taskId] = state with { Result = result };
                }

                _logBacktestCompleted(_logger, taskId, null);
            }
            catch (OperationCanceledException)
            {
                if (_activeTasks.TryRemove(taskId, out var state))
                {
                    state.DisposeCts();
                    state.DisposeMappedTickLists();
                }
                _logBacktestCancelled(_logger, taskId, null);
            }
            catch (Exception ex)
            {
                if (_activeTasks.TryRemove(taskId, out var state))
                {
                    state.DisposeCts();
                    state.DisposeMappedTickLists();
                }
                _logBacktestFailed(_logger, taskId, ex);
            }
            finally
            {
                // Ensure mapped lists are disposed even if task state was removed
                if (_activeTasks.TryGetValue(taskId, out var state))
                {
                    state.DisposeMappedTickLists();
                }
            }
        }, cts.Token);

        return taskId;
    }

    /// <inheritdoc/>
    public Task<BacktestResult> GetBacktestResultAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_activeTasks.TryGetValue(taskId, out var state) && state.Result is BacktestResult result)
        {
            return Task.FromResult(result);
        }
        return Task.FromResult(new BacktestResult { Balance = 0, Equity = 0, Drawdown = 0, DailyDrawdown = 0, TotalTrades = 0 });
    }

    /// <inheritdoc/>
    public async Task<string> StartLiveAsync(LiveInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        string taskId = $"live_{Guid.NewGuid():N}";
        _logLiveStarted(_logger, taskId, null);

        var adapter = _extensionManager.ActiveAdapter
            ?? throw new InvalidOperationException("No active adapter found.");
        var strategy = _extensionManager.ActiveStrategy
            ?? throw new InvalidOperationException("No active strategy found.");

        var symbolProperties = new Dictionary<string, SymbolProperties>();
        foreach (var sym in input.Symbols)
        {
            var props = await adapter.GetSymbolPropertiesAsync(sym, cancellationToken).ConfigureAwait(false);
            if (props != null)
            {
                symbolProperties[sym] = props;
            }
            else
            {
                throw new InvalidOperationException($"Symbol properties for {sym} not available.");
            }
        }

        var marketClock = new TickClock();
        var wallClock = new SystemClock();

        // CA2000 is suppressed because the CTS is stored in the task state and disposed
        // when the live session stops.
        CancellationTokenSource cts;
#pragma warning disable CA2000 // Dispose objects before losing scope
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
#pragma warning restore CA2000
        LiveBroker? liveBroker = null;

        try
        {
#pragma warning disable CA2000 // LiveBroker is stored in the task state and disposed when the live session stops
            liveBroker = new LiveBroker(
                adapter,
                input.MagicNumber,
                input.Leverage,
                marketClock,
                wallClock,
                _metrics,
                _messageBus,
                _hookRegistry.Live,
                input.OrderGuardTimeoutSeconds,
                input.StopOutLevel,
                syncIntervalTicks: TimeSpan.TicksPerMinute
            );
#pragma warning restore CA2000

            foreach (var kv in symbolProperties)
            {
                await liveBroker.SetSymbolSpecsAsync(kv.Key, kv.Value).ConfigureAwait(false);
            }

            var spec = new StrategySpecification
            {
                InitialBalance = input.InitialBalance,
                Leverage = input.Leverage,
                RequestedSymbols = input.Symbols.Select(s => new SymbolRequest(s, ImmutableArray.Create(TimeFrame.Tick))).ToImmutableArray()
            };
            await strategy.OnConfigureAsync(spec).ConfigureAwait(false);

            // Create and store the indicator registry for later disposal
            using (var tickWindow = new TickWindow(input.Symbols, new[] { TimeFrame.Tick }))
            {
                var indicatorRegistry = IndicatorRegistryFactory.Create(tickWindow);
                try
                {
                    await strategy.OnStartAsync(indicatorRegistry).ConfigureAwait(false);
                    // Store the registry in the task state
                    var taskState = new TaskState(cts, Broker: liveBroker, Strategy: strategy, IndicatorRegistry: indicatorRegistry);
                    try
                    {
                        _activeTasks[taskId] = taskState;
                    }
                    catch
                    {
                        cts.Dispose();
                        throw;
                    }
                }
                catch
                {
                    // If an error occurs, dispose the registry
                    if (indicatorRegistry is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    else
                    {
                        // Fallback: call DisposeAll via reflection
                        var method = indicatorRegistry.GetType().GetMethod("DisposeAll");
                        method?.Invoke(indicatorRegistry, null);
                    }
                    throw;
                }
            }

            strategy.InjectGenes(input.Genes);

            await liveBroker.ConnectAndNotifyAsync(cancellationToken).ConfigureAwait(false);
            await liveBroker.InitializeLiveStateAsync(cancellationToken).ConfigureAwait(false);

            // Subscribe to symbols and wire up tick handler
            Action<string, Tick> tickHandler = (symbol, tick) =>
            {
                try
                {
                    // Forward the tick to the live broker.
                    // This call is synchronous in the event handler; we wrap it in Task.Run
                    // to avoid blocking the adapter's event loop, but the broker's OnTickAsync
                    // is designed to be thread-safe.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await liveBroker.OnTickAsync(symbol, tick).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logLiveTickHandlerError(_logger, symbol, ex);
                        }
                    }, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logLiveTickHandlerError(_logger, symbol, ex);
                }
            };

            // Attach the handler to the adapter's event
            adapter.OnTickReceived += tickHandler;

            // Subscribe to each symbol
            foreach (var sym in input.Symbols)
            {
                await adapter.SubscribeAsync(sym).ConfigureAwait(false);
            }

            // Update the task state with the adapter, symbols, and tick handler for cleanup
            if (_activeTasks.TryGetValue(taskId, out var existingState))
            {
                _activeTasks[taskId] = existingState with
                {
                    Adapter = adapter,
                    Symbols = input.Symbols,
                    TickHandler = tickHandler
                };
            }

            return taskId;
        }
        catch
        {
            // If an exception occurs after liveBroker is created, dispose it.
            if (liveBroker != null)
            {
                await liveBroker.DisposeAsync().ConfigureAwait(false);
            }
            cts.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<LiveState> GetLiveStateAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_activeTasks.TryGetValue(taskId, out var state) && state.Broker is LiveBroker broker)
        {
            await broker.SyncStateAsync(cancellationToken).ConfigureAwait(false);
            return new LiveState
            {
                IsLive = true,
                Equity = broker.Equity,
                Balance = broker.Balance,
                Drawdown = broker.MaxDrawdown,
                Positions = Array.Empty<object>(),
                Orders = Array.Empty<object>()
            };
        }
        return new LiveState { IsLive = false, Equity = 0, Balance = 0 };
    }

    /// <inheritdoc/>
    public async Task StopLiveAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_activeTasks.TryRemove(taskId, out var state) && state.Broker is LiveBroker broker)
        {
            // Unsubscribe from the adapter's tick event
            if (state.Adapter != null && state.TickHandler != null)
            {
                state.Adapter.OnTickReceived -= state.TickHandler;
            }

            // Unsubscribe from all symbols
            if (state.Adapter != null && state.Symbols != null)
            {
                foreach (var sym in state.Symbols)
                {
                    try
                    {
                        await state.Adapter.UnsubscribeAsync(sym).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logFailedUnsubscribe(_logger, sym, ex);
                    }
                }
            }

            state.DisposeCts();
            state.DisposeIndicatorRegistry();

            await broker.DisconnectAndNotifyAsync().ConfigureAwait(false);
            await broker.DisposeAsync().ConfigureAwait(false);

            _logLiveStopped(_logger, taskId, null);
        }
    }

    /// <inheritdoc/>
    public Task PauseLiveAsync(string taskId, CancellationToken cancellationToken)
    {
        _logPauseLiveNotImplemented(_logger, null);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResumeLiveAsync(string taskId, CancellationToken cancellationToken)
    {
        _logResumeLiveNotImplemented(_logger, null);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task InjectGenesAsync(string taskId, double[] genes, CancellationToken cancellationToken)
    {
        if (_activeTasks.TryGetValue(taskId, out var state) && state.Strategy is IStrategyCapability strategy)
        {
            strategy.InjectGenes(genes);
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> StartOptimizationAsync(OptimizationInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        string taskId = $"opt_{Guid.NewGuid():N}";
        _logOptimizationStarted(_logger, taskId, null);

        var adapter = _extensionManager.ActiveAdapter
            ?? throw new InvalidOperationException("No active adapter found.");
        var strategy = _extensionManager.ActiveStrategy
            ?? throw new InvalidOperationException("No active strategy found.");

        var symbolProperties = new Dictionary<string, SymbolProperties>();
        foreach (var sym in input.Symbols)
        {
            var props = await adapter.GetSymbolPropertiesAsync(sym, cancellationToken).ConfigureAwait(false);
            if (props != null)
            {
                symbolProperties[sym] = props;
            }
            else
            {
                throw new InvalidOperationException($"Symbol properties for {sym} not available.");
            }
        }

        var optSpec = new OptimizationSpecification
        {
            MasterSeed = input.MasterSeed,
            Generations = input.Generations,
            PopulationSize = input.PopulationSize,
            MutationRate = input.MutationRate,
            CrossoverRate = input.CrossoverRate,
            ElitismPct = input.ElitismPct,
            TournamentSize = input.TournamentSize,
            StagnationGenerationsBeforeHyper = input.StagnationGenerationsBeforeHyper,
            MaxParallelThreads = input.MaxParallelThreads
        };

        var schema = GeneInjector.ExtractSchema(strategy.GetType());

        var runner = new OptimizationRunner(
            optSpec,
            schema,
            (CoreMetrics)_metrics,
            _hookRegistry.Optimization,
            _extensionManager.ActiveNeuralNetwork,
            _messageBus,
            _hookRegistry
        );

        // Define fitness evaluator
        async Task<double> EvaluateChromosome(ChromosomeKernel chromo, CancellationToken ct)
        {
            strategy.InjectGenes(chromo.Genes);

            // Build backtest input
            var backtestInput = new BacktestInput
            {
                TickStreams = Array.Empty<IReadOnlyList<Tick>>(),
                Symbols = input.Symbols,
                Strategy = strategy,
                StrategySpecification = new StrategySpecification
                {
                    InitialBalance = input.InitialBalance,
                    Leverage = input.Leverage,
                    RequestedSymbols = input.Symbols.Select(s => new SymbolRequest(s, ImmutableArray.Create(TimeFrame.Tick))).ToImmutableArray()
                },
                ExecutionSpecification = new ExecutionSpecification
                {
                    StartDate = DateTime.UtcNow.AddDays(-30),
                    EndDate = DateTime.UtcNow,
                    WarmupWindowCount = 0,
                    MaxOpenPositions = 5,
                    StopOutLevel = 0.5,
                    LatencyTicks = 0,
                    MaxParallelThreads = 1
                },
                MarketCalculator = adapter.Calculator,
                SymbolProperties = symbolProperties,
                Genes = chromo.Genes,
                NeuralNetwork = _extensionManager.ActiveNeuralNetwork,
                MessageBus = _messageBus,
                Metrics = _metrics,
                HookRegistry = _hookRegistry
            };

            var runnerBt = new BacktestRunner();
            var result = await runnerBt.RunAsync(backtestInput, ct).ConfigureAwait(false);
            return result.Balance - input.InitialBalance;
        }

        // CA2000 is suppressed because the CTS is stored in the task state and disposed
        // when the optimisation completes or is cancelled.
        CancellationTokenSource cts;
#pragma warning disable CA2000 // Dispose objects before losing scope
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
#pragma warning restore CA2000
        var taskState = new TaskState(cts, Runner: runner);

        try
        {
            _activeTasks[taskId] = taskState;
        }
        catch
        {
            cts.Dispose();
            throw;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var best = await runner.RunAsync(EvaluateChromosome, cts.Token).ConfigureAwait(false);
                _activeTasks[taskId] = _activeTasks[taskId] with { Result = best };
                _logOptimizationCompleted(_logger, taskId, null);
            }
            catch (OperationCanceledException)
            {
                if (_activeTasks.TryRemove(taskId, out var state))
                {
                    state.DisposeCts();
                }
                _logOptimizationCancelled(_logger, taskId, null);
            }
            catch (Exception ex)
            {
                if (_activeTasks.TryRemove(taskId, out var state))
                {
                    state.DisposeCts();
                }
                _logOptimizationFailed(_logger, taskId, ex);
            }
        }, cts.Token);

        return taskId;
    }

    /// <inheritdoc/>
    public Task<ChromosomeKernel> GetOptimizationResultAsync(string taskId, CancellationToken cancellationToken)
    {
        if (_activeTasks.TryGetValue(taskId, out var state) && state.Result is ChromosomeKernel best)
        {
            return Task.FromResult(best);
        }

        // Return a dummy chromosome
        var dummy = new ChromosomeKernel(1);
        dummy.Genes[0] = 0;
        dummy.Fitness = 0;
        dummy.Generation = 0;
        dummy.IndividualIndex = 0;
        dummy.Seed = 0;
        return Task.FromResult(dummy);
    }

    private static TimeFrame ParseTimeFrame(string tf)
    {
        return tf.ToUpperInvariant() switch
        {
            "TICK" => TimeFrame.Tick,
            "M1" => TimeFrame.M1,
            "M5" => TimeFrame.M5,
            "M15" => TimeFrame.M15,
            "M30" => TimeFrame.M30,
            "H1" => TimeFrame.H1,
            "H2" => TimeFrame.H2,
            "H3" => TimeFrame.H3,
            "H4" => TimeFrame.H4,
            "H6" => TimeFrame.H6,
            "H12" => TimeFrame.H12,
            "D1" => TimeFrame.D1,
            "D2" => TimeFrame.D2,
            "D3" => TimeFrame.D3,
            "W1" => TimeFrame.W1,
            "MN1" => TimeFrame.MN1,
            _ => TimeFrame.Tick
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _taskLock.Dispose();
        foreach (var kv in _activeTasks)
        {
            kv.Value.DisposeCts();
            kv.Value.DisposeIndicatorRegistry();
            kv.Value.DisposeMappedTickLists();
        }
        _activeTasks.Clear();
    }
}
