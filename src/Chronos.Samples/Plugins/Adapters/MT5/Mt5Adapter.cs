using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Chronos.Abstractions.Adapters;
using Chronos.Abstractions.Shared;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // LoggerMessage delegates are not required for samples.

namespace Chronos.Samples.Plugins.Adapters.MT5;

/// <summary>
/// Production‑ready adapter for MetaTrader 5 terminals.
/// Communicates with a companion MQL5 bridge EA via TCP.
/// </summary>
[AdapterName("MT5")]
[SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by the Chronos plugin loader via attribute discovery.")]
internal sealed class Mt5Adapter : IAdapter, IDisposable
{
    private readonly ILogger<Mt5Adapter> _logger;
    private readonly Mt5AdapterConfiguration _config;
    private readonly Mt5MarketCalculator _calculator = new();

    private Mt5BridgeClient? _bridge;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _subscriptions = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, SymbolProperties> _symbolPropsCache =
        new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _maintenanceCts;
    private Task? _maintenanceTask;
    private bool _disposed;
    private volatile bool _isConnected;

    // === ADDED: track retention policy per generated file ===
    private readonly ConcurrentDictionary<string, DataActionPolicy> _filePolicies = new();
    // =======================================================

    /// <inheritdoc/>
    public string AdapterName => "MT5";

    /// <inheritdoc/>
    public IMarketCalculator Calculator => _calculator;

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public event Action<string, Tick>? OnTickReceived;

    /// <inheritdoc/>
    public event Action<ExecutionReport>? OnExecutionUpdate;

    /// <summary>
    /// Tracks historical data files generated during this adapter's lifecycle.
    /// </summary>
    private readonly ConcurrentBag<string> _sessionGeneratedFiles = new();

    public Mt5Adapter(Mt5AdapterConfiguration config, ILogger<Mt5Adapter>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Validate();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Mt5Adapter>.Instance;
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isConnected) return true;

            // Establish initial connection
            await ConnectBridgeWithRetryAsync(cancellationToken).ConfigureAwait(false);
            _isConnected = true;

            // Start the background reconnection loop
            _maintenanceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _maintenanceTask = Task.Run(() => MaintenanceLoopAsync(_maintenanceCts.Token), _maintenanceCts.Token);

            _logger.LogInformation("MT5 adapter connected.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MT5 connection failed.");
            _isConnected = false;
            throw new AdapterException(AdapterName, "Connection failed", ex);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        // Cancel the maintenance loop
        if (_maintenanceCts is not null)
            await _maintenanceCts.CancelAsync().ConfigureAwait(false);

        if (_maintenanceTask is not null)
            await _maintenanceTask.ConfigureAwait(false);

        await ShutdownBridgeAsync().ConfigureAwait(false);
        _isConnected = false;
        _logger.LogInformation("MT5 adapter disconnected.");
    }

    // ------------------------------------------------------------------------
    // Maintenance loop – keeps the TCP connection alive
    // ------------------------------------------------------------------------
    private async Task MaintenanceLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // If bridge is null or not connected, reconnect
                if (_bridge is null || !_isConnected)
                {
                    _logger.LogWarning("Bridge disconnected, reconnecting...");
                    await ConnectBridgeWithRetryAsync(ct).ConfigureAwait(false);
                    _isConnected = true;

                    // Re‑subscribe to all previously subscribed symbols
                    var bridge = _bridge!; // guaranteed non-null after ConnectBridgeWithRetryAsync succeeds
                    foreach (var sym in _subscriptions.Values)
                        await bridge.SendCommandAsync<object>("subscribe", new { symbol = sym }, ct)
                            .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconnection failed, retrying in 3s...");
                await Task.Delay(3000, ct).ConfigureAwait(false);
            }
#pragma warning restore CA1031
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Reconnection loop must retry on any transient error.")]
    private async Task ConnectBridgeWithRetryAsync(CancellationToken ct)
    {
        // Clean up any existing bridge
        await ShutdownBridgeAsync().ConfigureAwait(false);

        _bridge = new Mt5BridgeClient(_config);
        _bridge.OnTick += (symbol, tick) => OnTickReceived?.Invoke(symbol, tick);
        // TODO: hook OnExecutionUpdate when the EA supports it
        _bridge.OnExecutionUpdate += report => OnExecutionUpdate?.Invoke(report);

        int attempts = 0;
        while (true)
        {
            attempts++;
            try
            {
                await _bridge.ConnectAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("Bridge connected.");
                return;
            }
            catch when (attempts < _config.MaxReconnectAttempts)
            {
                _logger.LogWarning("Bridge connection attempt {Attempt} failed, retrying in {Delay}ms...",
                    attempts, _config.ReconnectIntervalMs);
                await Task.Delay(_config.ReconnectIntervalMs, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bridge connection failed after {Attempts} attempts.", attempts);
                throw new AdapterException(AdapterName, "Could not connect to bridge EA", ex);
            }
        }
    }

    private async Task ShutdownBridgeAsync()
    {
        if (_bridge is null) return;
        await _bridge.DisconnectAsync().ConfigureAwait(false);
        await _bridge.DisposeAsync().ConfigureAwait(false);
        _bridge = null;
    }

    // ------------------------------------------------------------------------
    // IHistoricalDataProvider
    // ------------------------------------------------------------------------
    public async Task<HistoricalDataResponse> FetchHistoryToBinaryFileAsync(
        HistoricalDataRequest request, CancellationToken cancellationToken)
    {
        EnsureConnected();

        string startStr = request.StartTime.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);
        string endStr = request.EndTime.ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture);

        var payload = new { symbol = request.Symbol, startTime = startStr, endTime = endStr };

        var response = await _bridge!.SendCommandAsync<HistoryFetchResponse>(
            "fetchHistory", payload, cancellationToken).ConfigureAwait(false);

        if (response == null || !response.Success || string.IsNullOrEmpty(response.FilePath))
        {
            return new HistoricalDataResponse { Success = false, ErrorMessage = response?.Error ?? "Unknown error" };
        }

        string destinationPath = Path.Combine(
            _config.HistoryCachePath,
            $"{request.Symbol}_{request.StartTime:yyyyMMdd}_{request.EndTime:yyyyMMdd}.bin");

        Directory.CreateDirectory(_config.HistoryCachePath);

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        // This cuts the file out of MT5's directory and moves it to Chronos cache
        File.Move(response.FilePath, destinationPath);

        // === ADDED: store the retention policy for this file ===
        _filePolicies[destinationPath] = request.RetentionPolicy;
        // =====================================================

        // Track file if it needs active lifecycle cleanup based on policy
        if (request.RetentionPolicy is DataActionPolicy.DeleteAfterTask or DataActionPolicy.KeepUntilExit)
        {
            _sessionGeneratedFiles.Add(destinationPath);
        }

        return new HistoricalDataResponse
        {
            Symbol = request.Symbol,
            Success = true,
            BinaryFilePath = destinationPath,
            TotalRecords = response.TotalRecords
        };
    }

    public Task DeleteHistoryFileAsync(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    // === REPLACED NotifyFileSafeToDeleteAsync implementation ===
    public Task NotifyFileSafeToDeleteAsync(string filePath)
    {
        if (_filePolicies.TryGetValue(filePath, out var policy) && policy != DataActionPolicy.PersistentCache)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "I/O error deleting historical file {Path}", filePath);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "Permission error deleting historical file {Path}", filePath);
                }
            });
        }

        return Task.CompletedTask;
    }
    // ============================================================

    // ------------------------------------------------------------------------
    // ILiveDataProvider
    // ------------------------------------------------------------------------
    public async Task SubscribeAsync(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        string sym = symbol.ToUpperInvariant();
        if (_subscriptions.TryAdd(sym, sym))
        {
            EnsureConnected();
            await _bridge!.SendCommandAsync<object>("subscribe", new { symbol = sym }, _maintenanceCts!.Token)
                .ConfigureAwait(false);
        }
    }

    public async Task UnsubscribeAsync(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        string sym = symbol.ToUpperInvariant();
        if (_subscriptions.TryRemove(sym, out _))
        {
            if (_bridge?.IsConnected is true)
                await _bridge.SendCommandAsync<object>("unsubscribe", new { symbol = sym }, _maintenanceCts!.Token)
                    .ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------------
    // IExecutionProvider
    // ------------------------------------------------------------------------
    public async Task<AdapterOrderResponse> ExecuteOrderAsync(AdapterOrderRequest request)
    {
        EnsureConnected();
        return await _bridge!.SendCommandAsync<AdapterOrderResponse>(
                   "executeOrder", request, _maintenanceCts!.Token).ConfigureAwait(false)
               ?? new AdapterOrderResponse { Success = false, ErrorMessage = "No response" };
    }

    public async Task<AdapterOrderResponse> ModifyOrderAsync(
        long ticket, double? sl = null, double? tp = null, double? price = null)
    {
        EnsureConnected();
        var payload = new { ticket, sl, tp, price };
        return await _bridge!.SendCommandAsync<AdapterOrderResponse>(
                   "modifyOrder", payload, _maintenanceCts!.Token).ConfigureAwait(false)
               ?? new AdapterOrderResponse { Success = false, ErrorMessage = "No response" };
    }

    public async Task<AdapterOrderResponse> ClosePositionAsync(long ticket, double? volume = null)
    {
        EnsureConnected();
        var payload = new { ticket, volume };
        return await _bridge!.SendCommandAsync<AdapterOrderResponse>(
                   "closePosition", payload, _maintenanceCts!.Token).ConfigureAwait(false)
               ?? new AdapterOrderResponse { Success = false, ErrorMessage = "No response" };
    }

    public async Task<AdapterOrderResponse> CancelAsync(long ticket)
    {
        EnsureConnected();
        var payload = new { ticket };
        return await _bridge!.SendCommandAsync<AdapterOrderResponse>(
                   "cancelOrder", payload, _maintenanceCts!.Token).ConfigureAwait(false)
               ?? new AdapterOrderResponse { Success = false, ErrorMessage = "No response" };
    }

    public async Task<(double Balance, double Equity)> GetAccountInfoAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        var resp = await _bridge!.SendCommandAsync<AccountInfoResponse>(
            "accountInfo", null!, cancellationToken).ConfigureAwait(false);
        return resp != null ? (resp.Balance, resp.Equity) : (0, 0);
    }

    public async Task<IReadOnlyList<Position>> GetActivePositionsAsync()
    {
        EnsureConnected();
        var resp = await _bridge!.SendCommandAsync<PositionsResponse>(
            "getPositions", null!, _maintenanceCts!.Token).ConfigureAwait(false);
        return resp?.Positions ?? [];
    }

    public async Task<IReadOnlyList<Order>> GetPendingOrdersAsync()
    {
        EnsureConnected();
        var resp = await _bridge!.SendCommandAsync<OrdersResponse>(
            "getPendingOrders", null!, _maintenanceCts!.Token).ConfigureAwait(false);
        return resp?.Orders ?? [];
    }

    private static readonly TimeSpan SymbolPropsCacheTtl = TimeSpan.FromMinutes(5);

    public async Task<SymbolProperties?> GetSymbolPropertiesAsync(
        string symbol, CancellationToken cancellationToken = default)
    {
        if (_symbolPropsCache.TryGetValue(symbol, out var cached))
            return cached;

        EnsureConnected();
        var resp = await _bridge!.SendCommandAsync<SymbolPropertiesResponse>(
            "symbolProperties", new { symbol }, cancellationToken).ConfigureAwait(false);
        if (resp?.Props == null) return null;

        _symbolPropsCache[symbol] = resp.Props;
        _ = ExpireSymbolPropsAsync(symbol);
        return resp.Props;
    }

    private async Task ExpireSymbolPropsAsync(string symbol)
    {
        await Task.Delay(SymbolPropsCacheTtl).ConfigureAwait(false);
        _symbolPropsCache.TryRemove(symbol, out _);
    }

    public TimeFrame[]? GetSupportedTimeframes(string symbol) =>
    [
        TimeFrame.M1, TimeFrame.M5, TimeFrame.M15, TimeFrame.M30,
        TimeFrame.H1, TimeFrame.H2, TimeFrame.H3, TimeFrame.H4,
        TimeFrame.H6, TimeFrame.H12,
        TimeFrame.D1, TimeFrame.D2, TimeFrame.D3,
        TimeFrame.W1, TimeFrame.MN1
    ];

    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isConnected)
            throw new AdapterException(AdapterName, "Not connected to MT5 bridge.");
    }

    /// <summary>
    /// Call this explicitly when a backtest or optimization task completes
    /// to satisfy DataActionPolicy.DeleteAfterTask.
    /// </summary>
    public void OnTaskCompleted(DataActionPolicy currentPolicy)
    {
        if (currentPolicy != DataActionPolicy.DeleteAfterTask) return;

        while (_sessionGeneratedFiles.TryTake(out var filePath))
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error deleting historical file under DeleteAfterTask policy: {Path}",
                    filePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Permission error deleting historical file under DeleteAfterTask policy: {Path}",
                    filePath);
            }
        }
    }

    /// <summary>
    /// Cleans up any remaining files marked as KeepUntilExit when the engine completely shuts down.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Clean up files remaining for KeepUntilExit or forced cleanup on disposal
        while (_sessionGeneratedFiles.TryTake(out var filePath))
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error cleaning up file on disposal: {Path}", filePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Permission error cleaning up file on disposal: {Path}", filePath);
            }
        }

        _maintenanceCts?.Cancel();
        _bridge?.Dispose();
        _connectionLock.Dispose();
        _maintenanceCts?.Dispose();
    }

    // DTOs
    private sealed record HistoryFetchResponse(bool Success, string? Error, string? FilePath, long TotalRecords);

    private sealed record TickDto(long Time, double Bid, double Ask, double Volume, bool IsSynthetic);

    private sealed record AccountInfoResponse(double Balance, double Equity);

    private sealed record PositionsResponse(Position[] Positions);

    private sealed record OrdersResponse(Order[] Orders);

    private sealed record SymbolPropertiesResponse(SymbolProperties Props);
}
#pragma warning restore CA1848
