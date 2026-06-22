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

    private sealed record TaskState(
        CancellationTokenSource Cts,
        object? Result = null,
        LiveBroker? Broker = null,
        IStrategyCapability? Strategy = null,
        OptimizationRunner? Runner = null,
        IIndicatorRegistry? IndicatorRegistry = null)
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
    public async Task<string> StartBacktestAsync(BacktestInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        string taskId = $"bt_{Guid.NewGuid():N}";
        _logBacktestStarted(_logger, taskId, null);

#pragma warning disable CA2000 // Disposable is stored and disposed later in the task state
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeTasks[taskId] = new TaskState(cts);
#pragma warning restore CA2000

        _ = Task.Run(async () =>
        {
            try
            {
                var runner = new BacktestRunner();
                var result = await runner.RunAsync(input, cts.Token).ConfigureAwait(false);
                _activeTasks[taskId] = _activeTasks[taskId] with { Result = result };
                _logBacktestCompleted(_logger, taskId, null);
            }
            catch (OperationCanceledException)
            {
                if (_activeTasks.TryRemove(taskId, out var state))
                {
                    state.DisposeCts();
                }
                _logBacktestCancelled(_logger, taskId, null);
            }
            catch (Exception ex)
            {
                if (_activeTasks.TryRemove(taskId, out var state))
                {
                    state.DisposeCts();
                }
                _logBacktestFailed(_logger, taskId, ex);
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

#pragma warning disable CA2000 // LiveBroker ownership transferred to TaskState; disposed in StopLiveAsync
        var liveBroker = new LiveBroker(
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
#pragma warning disable CA2000 // Disposable is stored and disposed later in the task state
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeTasks[taskId] = new TaskState(cts, Broker: liveBroker, Strategy: strategy, IndicatorRegistry: indicatorRegistry);
#pragma warning restore CA2000
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

        foreach (var sym in input.Symbols)
        {
            await adapter.SubscribeAsync(sym).ConfigureAwait(false);
        }

        return taskId;
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

#pragma warning disable CA2000 // Disposable is stored and disposed later in the task state
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeTasks[taskId] = new TaskState(cts, Runner: runner);
#pragma warning restore CA2000

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
        }
        _activeTasks.Clear();
    }
}
