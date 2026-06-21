using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Chronos.Core.Engine.Services.Mining;

internal interface IMiningIntegration
{
    Task StartAsync(object config, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<object> GetStatusAsync(CancellationToken cancellationToken);
    Task UpdateConfigAsync(object config, CancellationToken cancellationToken);
}

internal sealed class MiningConfig
{
    public string PoolUrl { get; set; } = "stratum+tcp://btc.pool.com:3333";
    public string WalletAddress { get; set; } = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    public string Password { get; set; } = "x";
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
    public bool SimulateOnly { get; set; } = true;
}

internal sealed class MiningIntegration : IMiningIntegration, IDisposable
{
    private readonly ILogger<MiningIntegration> _logger;
    private bool _isMining;
    private MiningConfig? _config;
    private CancellationTokenSource? _cts;
    private Task? _task;
    private double _hashrate;
    private long _hashesComputed;
    private long _acceptedShares;
    private bool _disposed;

    private static readonly Action<ILogger, int, string, bool, Exception?> _logMiningStarted =
        LoggerMessage.Define<int, string, bool>(LogLevel.Information, 0, "Mining started (admin-only). Threads: {Threads}, Pool: {Pool}, SimulateOnly: {Simulate}");
    private static readonly Action<ILogger, int, Exception?> _logShareFound =
        LoggerMessage.Define<int>(LogLevel.Debug, 1, "Share found by worker {WorkerId}!");
    private static readonly Action<ILogger, Exception?> _logMiningStopError =
        LoggerMessage.Define(LogLevel.Error, 2, "Error during mining stop.");
    private static readonly Action<ILogger, Exception?> _logMiningStopped =
        LoggerMessage.Define(LogLevel.Information, 3, "Mining stopped by admin.");
    private static readonly Action<ILogger, bool, Exception?> _logMiningConfigUpdated =
        LoggerMessage.Define<bool>(LogLevel.Information, 4, "Mining configuration updated. SimulateOnly: {Simulate}");

    public MiningIntegration(ILogger<MiningIntegration> logger) => _logger = logger;

    public Task StartAsync(object config, CancellationToken cancellationToken)
    {
        if (_isMining)
        {
            return Task.CompletedTask;
        }

        _config = config as MiningConfig ?? new MiningConfig();
        _isMining = true;
        _cts = new CancellationTokenSource();
        _task = Task.Run(() => MineAsync(_cts.Token), _cts.Token);

        _logMiningStarted(_logger, _config.ThreadCount, _config.PoolUrl, _config.SimulateOnly, null);
        return Task.CompletedTask;
    }

    private async Task MineAsync(CancellationToken token)
    {
        int threads = Math.Max(1, _config?.ThreadCount ?? Environment.ProcessorCount);
        var tasks = new Task[threads];
        for (int i = 0; i < threads; i++)
        {
            tasks[i] = MineWorkerAsync(i, threads, token);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task MineWorkerAsync(int workerId, int totalThreads, CancellationToken token)
    {
        using var rng = RandomNumberGenerator.Create();
        var header = new byte[80];
        var target = new byte[32];
        target[0] = 0x00;
        target[1] = 0x00;
        target[2] = 0x0F;
        target[3] = 0xFF;

        while (!token.IsCancellationRequested && _isMining)
        {
            var nonceBytes = new byte[4];
            rng.GetBytes(nonceBytes);
            Buffer.BlockCopy(nonceBytes, 0, header, 76, 4);

            var hash1 = SHA256.HashData(header);
            var hash2 = SHA256.HashData(hash1);

            bool shareFound = false;
            for (int i = 0; i < 32; i++)
            {
                if (hash2[i] < target[i]) { shareFound = true; break; }
                if (hash2[i] > target[i])
                {
                    break;
                }
            }

            Interlocked.Increment(ref _hashesComputed);
            if (shareFound)
            {
                Interlocked.Increment(ref _acceptedShares);
                _logShareFound(_logger, workerId, null);
            }

            if (Interlocked.Read(ref _hashesComputed) % 1000 == 0)
            {
                _hashrate = totalThreads * 1_000_000.0;
            }

            await Task.Delay(0, token).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_isMining)
        {
            return;
        }

        _isMining = false;

        if (_cts != null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_task != null)
        {
            try
            {
                await _task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logMiningStopError(_logger, ex);
            }
        }

        _cts?.Dispose();
        _cts = null;
        _task = null;
        _logMiningStopped(_logger, null);
    }

    public Task<object> GetStatusAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(new
        {
            IsMining = _isMining,
            Hashrate = _hashrate,
            HashesComputed = Interlocked.Read(ref _hashesComputed),
            AcceptedShares = Interlocked.Read(ref _acceptedShares),
            Pool = _config?.PoolUrl ?? "Not Configured",
            Wallet = _config?.WalletAddress ?? "Not Set",
            Threads = _config?.ThreadCount ?? 0,
            SimulateOnly = _config?.SimulateOnly ?? true
        });
    }

    public Task UpdateConfigAsync(object config, CancellationToken cancellationToken)
    {
        _config = config as MiningConfig ?? new MiningConfig();
        _logMiningConfigUpdated(_logger, _config.SimulateOnly, null);

        if (_isMining)
        {
            _ = Task.Run(async () =>
            {
                await StopAsync(cancellationToken).ConfigureAwait(false);
                await StartAsync(_config, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _task?.Dispose();
    }
}
