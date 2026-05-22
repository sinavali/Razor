// Chronos/src/Chronos.Samples/Plugins/Strategies/CfdSmtDivergence/CfdSmtDivergenceStrategy.cs

using System.Collections.Concurrent;
using Chronos.Abstractions.Shared;
using Chronos.Abstractions.Strategies;

namespace Chronos.Samples.Plugins.Strategies.CfdSmtDivergence;

/// <summary>
/// Trading strategy that detects SMT divergence across multiple CFD symbols.
/// Uses a pair‑based approach, comparing all symbol combinations automatically.
///
/// Key features:
/// - Session and WorkTime filters (temporarily disabled for debugging).
/// - Volume auto‑calculated to risk 0.2% of equity per trade.
/// - Stop loss placed at recent swing high/low plus a full pip cushion.
/// - Optional feed‑forward neural network that filters divergence signals.
/// - Genome size &gt; 30 (49 genes originally, now 12 property genes + 36 NN weights = 48 genes).
/// </summary>
internal sealed class CfdSmtDivergenceStrategy : StrategyBase
{
    // ---------------------------------------------------------------
    // Genes – Non‑NN parameters (12 genes)
    // ---------------------------------------------------------------

    // Removed MinWindowCount gene (unused).

    /// <summary>Number of recent bars kept in memory for divergence comparison.</summary>
    [Gene(2, 20, step: 1, type: GeneType.Discrete, Name = "Lookback Window Count")]
    public int LookbackWindowCount { get; set; } = 2;

    /// <summary>Minimum price difference (in price units) to confirm divergence between two symbols.</summary>
    [Gene(0.00001, 0.001, step: 0.00002, type: GeneType.Continuous, Name = "Divergence Threshold (price)")]
    public double DivergenceThresholdPoints { get; set; } = 0.0001;

    /// <summary>Enable (1) or disable (0) the Asia trading session.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use Asia Session")]
    public int UseAsiaGene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the London trading session.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use London Session")]
    public int UseLondonGene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork 1 trading session.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NY1 Session")]
    public int UseNY1Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork 2 trading session.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NY2 Session")]
    public int UseNY2Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the London WorkTime 1 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use LWT1")]
    public int UseLWT1Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the London WorkTime 2 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use LWT2")]
    public int UseLWT2Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork WorkTime 1 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NYWT1")]
    public int UseNYWT1Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork WorkTime 2 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NYWT2")]
    public int UseNYWT2Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the NewYork WorkTime 3 window.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use NYWT3")]
    public int UseNYWT3Gene { get; set; } = 1;

    /// <summary>Enable (1) or disable (0) the neural network signal filter.</summary>
    [Gene(0, 1, step: 1, type: GeneType.Discrete, Name = "Use Neural Network")]
    public int UseNeuralNetworkGene { get; set; } = 1;

    // ---------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------
    private const TimeFrame DetectionTimeFrame = TimeFrame.M5;
    private const double RiskPercent = 0.002;
    private static readonly int[] NeuralTopology = { 5, 5, 1 };

    private readonly SessionFilter _sessionFilter = new();
    private readonly WorkTimeFilter _workTimeFilter = new();

    /// <summary>Per‑symbol history of recent bars (High, Low).</summary>
    private readonly ConcurrentDictionary<string, Queue<(double High, double Low)>> _history =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Symbol properties provided from outside (adapter).</summary>
    private Dictionary<string, SymbolProperties> _symbolProps = new(StringComparer.OrdinalIgnoreCase);

    private string[] _symbols = Array.Empty<string>();

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    /// <summary>
    /// Allows the backtest/optimisation runner to inject symbol properties.
    /// </summary>
    internal void SetSymbolProperties(Dictionary<string, SymbolProperties> props)
    {
        _symbolProps = props ?? throw new ArgumentNullException(nameof(props));
    }

    /// <inheritdoc/>
    public override Task OnConfigureAsync(StrategySpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.RequestedSymbols.Length < 2)
            throw new StrategyException(nameof(CfdSmtDivergenceStrategy),
                "At least two symbols required for pair‑based SMT divergence.");

        _symbols = spec.RequestedSymbols.Select(sr => sr.Symbol).ToArray();

        foreach (var sr in spec.RequestedSymbols)
        {
            if (!sr.TimeFrames.Contains(DetectionTimeFrame))
                throw new StrategyException(nameof(CfdSmtDivergenceStrategy),
                    $"Symbol {sr.Symbol} must include {DetectionTimeFrame} timeframe.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnStartAsync(IIndicatorRegistry indicators)
    {
        // Neural network will be created in InjectGenes if needed; do not create here.
        TickWindow.WindowCompleted += OnWindowCompleted;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task OnStopAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public override Task OnTickAsync(Tick tick) => Task.CompletedTask;

    // ---------------------------------------------------------------
    // Gene injection (fixed order and NN injection)
    // ---------------------------------------------------------------

    /// <inheritdoc/>
    public override void InjectGenes(double[] genes)
    {
        // Inject the 12 property genes first
        const int propertyGeneCount = 12;  // count of [Gene] properties above
        GeneInjector.InjectPropertyGenes(this, genes);

        // Update filter states
        _sessionFilter.UseAsia   = UseAsiaGene != 0;
        _sessionFilter.UseLondon = UseLondonGene != 0;
        _sessionFilter.UseNY1    = UseNY1Gene != 0;
        _sessionFilter.UseNY2    = UseNY2Gene != 0;

        _workTimeFilter.UseLWT1   = UseLWT1Gene != 0;
        _workTimeFilter.UseLWT2   = UseLWT2Gene != 0;
        _workTimeFilter.UseNYWT1  = UseNYWT1Gene != 0;
        _workTimeFilter.UseNYWT2  = UseNYWT2Gene != 0;
        _workTimeFilter.UseNYWT3  = UseNYWT3Gene != 0;

        // Handle NN
        if (UseNeuralNetworkGene != 0)
        {
            if (NeuralNet == null)
                NeuralNet = new FeedForwardNetwork(NeuralTopology, ActivationFunction.Tanh);
            // Inject the NN weight genes that come after the property genes
            GeneInjector.InjectNeuralGenes(NeuralNet, genes, propertyGeneCount);
        }
        else
        {
            NeuralNet = null;
        }
    }

    // ---------------------------------------------------------------
    // Window completion event handler
    // ---------------------------------------------------------------

    private void OnWindowCompleted(string symbol, TimeFrame tf)
    {
        if (tf != DetectionTimeFrame) return;

        if (!TickWindow.TryGetLastCompletedBar(symbol, tf, out _, out double high, out double low, out _, out _))
            return;

        var queue = _history.GetOrAdd(symbol, _ => new Queue<(double, double)>());
        lock (queue)
        {
            queue.Enqueue((high, low));
            while (queue.Count > LookbackWindowCount)
                queue.Dequeue();
        }

        _ = ProcessCompletedBarAsync();
    }

    private async Task ProcessCompletedBarAsync()
    {
        var symbols = _symbols;
        for (int i = 0; i < symbols.Length; i++)
        {
            for (int j = i + 1; j < symbols.Length; j++)
            {
                await ProcessPairAsync(symbols[i], symbols[j]).ConfigureAwait(false);
            }
        }
    }

    // ---------------------------------------------------------------
    // Pair processing
    // ---------------------------------------------------------------

    private async Task ProcessPairAsync(string symA, string symB)
    {
        if (!_history.TryGetValue(symA, out var qA) || !_history.TryGetValue(symB, out var qB))
            return;

        (double highA, double lowA) prevA, currA;
        (double highB, double lowB) prevB, currB;

        lock (qA) lock (qB)
        {
            if (qA.Count < 2 || qB.Count < 2)
                return;

            prevA = qA.ElementAt(qA.Count - 2);
            currA = qA.Last();
            prevB = qB.ElementAt(qB.Count - 2);
            currB = qB.Last();
        }

        bool bearish = (currA.highA > prevA.highA && currB.highB < prevB.highB) &&
                       (Math.Abs(currA.highA - prevA.highA) >= DivergenceThresholdPoints) &&
                       (Math.Abs(currB.highB - prevB.highB) >= DivergenceThresholdPoints);

        bool bullish = (currA.lowA < prevA.lowA && currB.lowB > prevB.lowB) &&
                       (Math.Abs(currA.lowA - prevA.lowA) >= DivergenceThresholdPoints) &&
                       (Math.Abs(currB.lowB - prevB.lowB) >= DivergenceThresholdPoints);

        if (!bearish && !bullish) return;

        if (UseNeuralNetworkGene != 0 && NeuralNet != null)
        {
            double[] inputs = BuildNeuralInputs(symA, bearish, bullish, prevA, currA);
            double[] outputs = NeuralNet.FeedForward(inputs);
            if (outputs.Length == 0 || outputs[0] <= 0)
                return;
        }

        bool isSell = bearish;
        string tradeSymbol = symA;

        if (await Broker.HasOpenPositionAsync(tradeSymbol).ConfigureAwait(false))
            return;

        if (!_symbolProps.TryGetValue(tradeSymbol, out var props))
            return;   // cannot trade without symbol properties

        await ExecuteTradeAsync(tradeSymbol, isSell,
            (isSell ? currA.highA : currA.lowA),
            (isSell ? currB.highB : currB.lowB),
            props).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------
    // Neural network input construction
    // ---------------------------------------------------------------

    private double[] BuildNeuralInputs(string symA, bool bearish, bool bullish,
        (double High, double Low) prevA, (double High, double Low) currA)
    {
        long tickTime = GetLastTickTime(symA);
        double session = _sessionFilter.IsInSession(tickTime);
        double worktime = _workTimeFilter.IsInWorkTime(tickTime);

        double bullishStrength = bullish
            ? Math.Clamp((prevA.Low - currA.Low) / DivergenceThresholdPoints, 0, 1)
            : 0;
        double bearishStrength = bearish
            ? Math.Clamp((currA.High - prevA.High) / DivergenceThresholdPoints, 0, 1)
            : 0;

        double barRange = currA.High - currA.Low;
        double normalizedRange = DivergenceThresholdPoints > 1e-8
            ? Math.Clamp(barRange / DivergenceThresholdPoints, 0, 5)
            : 0;

        return new[] { session, worktime, bullishStrength, bearishStrength, normalizedRange };
    }

    // ---------------------------------------------------------------
    // Trade execution (uses symbol properties for pip size)
    // ---------------------------------------------------------------

    private async Task ExecuteTradeAsync(string symbol, bool isSell, double highA, double highB, SymbolProperties props)
    {
        var recentTicks = TickWindow.GetRecentTicks(symbol, 1);
        if (recentTicks.Count == 0) return;
        Tick lastTick = recentTicks[^1];   // get the most recent tick

        double entryPrice = isSell ? lastTick.Bid : lastTick.Ask;
        if (entryPrice <= 0) return;

        double rawStopLevel = isSell ? Math.Max(highA, highB) : Math.Min(highA, highB);
        double tickSize = props.TickSize;
        double safetyCushion = tickSize;            // one tick of safety

        double stopPrice;
        if (isSell)
            stopPrice = Math.Max(rawStopLevel, entryPrice + safetyCushion) + safetyCushion;
        else
            stopPrice = Math.Min(rawStopLevel, entryPrice - safetyCushion) - safetyCushion;

        double slDistanceAbs = Math.Abs(entryPrice - rawStopLevel);
        if (slDistanceAbs <= 0) return;

        // Simplified pip distance: distance in ticks
        double slDistanceTicks = slDistanceAbs / tickSize;
        double pipValuePerLot = props.TickValue;    // value of one tick per contract
        if (pipValuePerLot <= 0) pipValuePerLot = 10.0;

        double equity = Broker.Equity;
        if (equity <= 0) return;

        double riskAmount = equity * RiskPercent;
        double volumeLots = riskAmount / (slDistanceTicks * pipValuePerLot);
        volumeLots = Math.Round(Math.Max(props.MinVolume, Math.Min(volumeLots, 50)), 2, MidpointRounding.AwayFromZero);

        OrderType orderType = isSell ? OrderType.Sell : OrderType.Buy;

        await Broker.ExecuteMarketOrderAsync(symbol, orderType, volumeLots,
            sl: stopPrice, tp: 0, comment: "SMT Divergence").ConfigureAwait(false);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private long GetLastTickTime(string symbol)
    {
        var ticks = TickWindow.GetRecentTicks(symbol, 1);
        return ticks.Count > 0 ? ticks[0].Time : DateTime.UtcNow.Ticks;
    }
}
