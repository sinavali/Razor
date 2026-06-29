using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronos.Core.Engine.Services.Mining;

/// <summary>Configuration for a mining session.</summary>
internal sealed class MiningConfig
{
    public string PoolUrl { get; set; } = "stratum+tcp://btc.pool.com:3333";
    public string WalletAddress { get; set; } = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    public string Password { get; set; } = "x";
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
}

/// <summary>Manages the mining lifecycle.</summary>
internal interface IMiningIntegration
{
    Task StartAsync(object config, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<object> GetStatusAsync(CancellationToken cancellationToken);
    Task UpdateConfigAsync(object config, CancellationToken cancellationToken);
}

/// <summary>Real Stratum‑based Bitcoin miner.</summary>
internal sealed class MiningIntegration : IMiningIntegration, IDisposable
{
    private readonly ILogger<MiningIntegration> _logger;
    private CancellationTokenSource? _cts;
    private Task? _miningTask;
    private MiningConfig? _config;
    private bool _isMining;
    private long _hashesComputed;
    private long _acceptedShares;
    private long _rejectedShares;
    private long _lastHashrateUpdate;
    private double _hashrate;
    private bool _disposed;

    private static readonly Action<ILogger, int, string, Exception?> _logMiningStarted =
        LoggerMessage.Define<int, string>(LogLevel.Information, 0, "Mining started with {Threads} threads on pool {Pool}.");

    private static readonly Action<ILogger, Exception?> _logMiningStopped =
        LoggerMessage.Define(LogLevel.Information, 1, "Mining stopped.");

    private static readonly Action<ILogger, string, Exception?> _logPoolConnectionFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 2, "Failed to connect to pool: {Error}");

    private static readonly Action<ILogger, string, Exception?> _logShareSubmitted =
        LoggerMessage.Define<string>(LogLevel.Debug, 3, "Share submitted: {Status}");

    private static readonly Action<ILogger, Exception?> _logWorkReceived =
        LoggerMessage.Define(LogLevel.Debug, 4, "New mining job received.");

    private static readonly Action<ILogger, string, Exception?> _logInvalidPoolUrl =
        LoggerMessage.Define<string>(LogLevel.Error, 5, "Invalid pool URL: {Url}");

    private static readonly Action<ILogger, Exception?> _logMiningStopError =
        LoggerMessage.Define(LogLevel.Error, 6, "Error stopping mining task.");

    public MiningIntegration(ILogger<MiningIntegration> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task StartAsync(object config, CancellationToken cancellationToken)
    {
        if (_isMining)
        {
            return Task.CompletedTask;
        }

        _config = ParseConfig(config);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _miningTask = Task.Run(() => RunMiningAsync(_cts.Token), _cts.Token);
        _isMining = true;
        _logMiningStarted(_logger, _config.ThreadCount, _config.PoolUrl, null);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
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

        if (_miningTask != null)
        {
            try
            {
                await _miningTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (Exception ex)
            {
                _logMiningStopError(_logger, ex);
            }
        }

        _cts?.Dispose();
        _cts = null;
        _miningTask = null;
        _logMiningStopped(_logger, null);
    }

    /// <inheritdoc/>
    public Task<object> GetStatusAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<object>(new
        {
            IsMining = _isMining,
            Hashrate = _hashrate,
            HashesComputed = Interlocked.Read(ref _hashesComputed),
            AcceptedShares = Interlocked.Read(ref _acceptedShares),
            RejectedShares = Interlocked.Read(ref _rejectedShares),
            Pool = _config?.PoolUrl ?? "Not Configured",
            Wallet = _config?.WalletAddress ?? "Not Set",
            Threads = _config?.ThreadCount ?? 0
        });
    }

    /// <inheritdoc/>
    public async Task UpdateConfigAsync(object config, CancellationToken cancellationToken)
    {
        if (!_isMining)
        {
            _config = ParseConfig(config);
            return;
        }

        // Stop and restart with new config
        await StopAsync(cancellationToken).ConfigureAwait(false);
        await StartAsync(config, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _miningTask?.Wait(TimeSpan.FromSeconds(5));
        _miningTask?.Dispose();
    }

    private static MiningConfig ParseConfig(object config)
    {
        if (config is MiningConfig miningConfig)
        {
            return miningConfig;
        }

        if (config is Dictionary<string, object> dict)
        {
            var result = new MiningConfig();
            if (dict.TryGetValue("PoolUrl", out var poolObj) && poolObj is string poolUrl)
            {
                result.PoolUrl = poolUrl;
            }

            if (dict.TryGetValue("WalletAddress", out var walletObj) && walletObj is string wallet)
            {
                result.WalletAddress = wallet;
            }

            if (dict.TryGetValue("Password", out var passObj) && passObj is string password)
            {
                result.Password = password;
            }

            if (dict.TryGetValue("ThreadCount", out var threadObj) && threadObj is int threadCount)
            {
                result.ThreadCount = threadCount;
            }
            else if (dict.TryGetValue("ThreadCount", out var threadObjStr) && threadObjStr is string threadStr &&
                     int.TryParse(threadStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedThread))
            {
                result.ThreadCount = parsedThread;
            }

            return result;
        }

        // Fallback to default
        return new MiningConfig();
    }

    private async Task RunMiningAsync(CancellationToken ct)
    {
        int threads = Math.Max(1, _config?.ThreadCount ?? Environment.ProcessorCount);
        var tasks = new Task[threads];
        for (int i = 0; i < threads; i++)
        {
            tasks[i] = MineWorkerAsync(i, ct);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task MineWorkerAsync(int workerId, CancellationToken ct)
    {
        // Set thread priority to lowest
        Thread.CurrentThread.Priority = ThreadPriority.Lowest;

        // Parse pool URL
        if (_config == null || !TryParsePoolUrl(_config.PoolUrl, out string? host, out int port))
        {
            _logInvalidPoolUrl(_logger, _config?.PoolUrl ?? "null", null);
            return;
        }

        while (!ct.IsCancellationRequested && _isMining)
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(host!, port).ConfigureAwait(false);
                using var stream = tcp.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\n" };

                // Send mining.subscribe
                string subscribeId = Guid.NewGuid().ToString();
                var subscribeReq = new
                {
                    id = subscribeId,
                    method = "mining.subscribe",
                    @params = new object[] { $"chronos-miner/{workerId}" }
                };
                string subscribeJson = JsonSerializer.Serialize(subscribeReq);
                await writer.WriteLineAsync(subscribeJson).ConfigureAwait(false);
                await writer.FlushAsync(ct).ConfigureAwait(false);

                string? subscribeResp = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(subscribeResp))
                {
                    throw new InvalidOperationException("No subscribe response.");
                }

#pragma warning disable CA2016 // Forward CancellationToken
                using var subDoc = JsonDocument.Parse(subscribeResp);
#pragma warning restore CA2016
                if (!subDoc.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("Invalid subscribe result.");
                }

                string? extranonce1 = result.EnumerateArray().ElementAt(1).GetString();
                int extranonce2Size = result.EnumerateArray().ElementAt(2).GetInt32();

                if (string.IsNullOrEmpty(extranonce1))
                {
                    throw new InvalidOperationException("Missing extranonce1.");
                }

                // Send mining.authorize
                string authId = Guid.NewGuid().ToString();
                var authReq = new
                {
                    id = authId,
                    method = "mining.authorize",
                    @params = new object[] { _config.WalletAddress, _config.Password }
                };
                string authJson = JsonSerializer.Serialize(authReq);
                await writer.WriteLineAsync(authJson).ConfigureAwait(false);
                await writer.FlushAsync(ct).ConfigureAwait(false);

                string? authResp = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(authResp))
                {
                    throw new InvalidOperationException("No authorize response.");
                }

#pragma warning disable CA2016
                using var authDoc = JsonDocument.Parse(authResp);
#pragma warning restore CA2016
                if (!authDoc.RootElement.TryGetProperty("result", out var authResult) || !authResult.GetBoolean())
                {
                    throw new InvalidOperationException("Authorization failed.");
                }

                // Mining loop – read notifications (mining.notify) and mine
                while (!ct.IsCancellationRequested && _isMining)
                {
                    string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(line))
                    {
                        // Connection closed
                        break;
                    }

#pragma warning disable CA2016
                    using var doc = JsonDocument.Parse(line);
#pragma warning restore CA2016
                    if (!doc.RootElement.TryGetProperty("method", out var methodProp) ||
                        methodProp.GetString() != "mining.notify")
                    {
                        // Ignore other notifications (e.g., minig.set_difficulty)
                        continue;
                    }

                    _logWorkReceived(_logger, null);

                    // Parse work parameters
                    if (!doc.RootElement.TryGetProperty("params", out var paramsArr) ||
                        paramsArr.ValueKind != JsonValueKind.Array || paramsArr.GetArrayLength() < 9)
                    {
                        continue;
                    }

                    string jobId = paramsArr[0].GetString() ?? string.Empty;
                    string prevHash = paramsArr[1].GetString() ?? string.Empty;
                    string coinbase1 = paramsArr[2].GetString() ?? string.Empty;
                    string coinbase2 = paramsArr[3].GetString() ?? string.Empty;
                    string[] merkleBranches = paramsArr[4].EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
                    string version = paramsArr[5].GetString() ?? string.Empty;
                    string nBits = paramsArr[6].GetString() ?? string.Empty;
                    string nTime = paramsArr[7].GetString() ?? string.Empty;
                    bool cleanJobs = paramsArr[8].GetBoolean();

                    // Build coinbase (coinbase1 + extranonce1 + extranonce2 + coinbase2)
                    string extranonce2 = GenerateExtranonce2(extranonce2Size);
                    string coinbase = coinbase1 + extranonce1 + extranonce2 + coinbase2;

                    // Compute merkle root
                    byte[] coinbaseBytes = HexToBytes(coinbase);
                    byte[] coinbaseHash = DoubleSHA256(coinbaseBytes);
                    byte[] merkleRoot = ComputeMerkleRoot(coinbaseHash, merkleBranches);

                    // Build block header: version + prevHash + merkleRoot + nTime + nBits + nonce (4 bytes)
                    byte[] versionBytes = HexToBytes(version);
                    Array.Reverse(versionBytes); // little-endian
                    byte[] prevHashBytes = HexToBytes(prevHash);
                    Array.Reverse(prevHashBytes);
                    byte[] merkleRootBytes = merkleRoot; // already little-endian from ComputeMerkleRoot
                    byte[] nTimeBytes = HexToBytes(nTime);
                    Array.Reverse(nTimeBytes);
                    byte[] nBitsBytes = HexToBytes(nBits);
                    Array.Reverse(nBitsBytes);

                    // Target for difficulty
                    uint target = BitsToTarget(nBitsBytes);
                    ulong maxNonce = uint.MaxValue;

                    // Mine: increment nonce
                    bool found = false;
                    for (uint nonce = 0; nonce < maxNonce; nonce++)
                    {
                        if (ct.IsCancellationRequested || !_isMining)
                        {
                            found = false;
                            break;
                        }

                        byte[] nonceBytes = BitConverter.GetBytes(nonce);
                        if (BitConverter.IsLittleEndian)
                        {
                            // already little-endian
                        }
                        else
                        {
                            Array.Reverse(nonceBytes);
                        }

                        byte[] header = new byte[80];
                        Buffer.BlockCopy(versionBytes, 0, header, 0, 4);
                        Buffer.BlockCopy(prevHashBytes, 0, header, 4, 32);
                        Buffer.BlockCopy(merkleRootBytes, 0, header, 36, 32);
                        Buffer.BlockCopy(nTimeBytes, 0, header, 68, 4);
                        Buffer.BlockCopy(nBitsBytes, 0, header, 72, 4);
                        Buffer.BlockCopy(nonceBytes, 0, header, 76, 4);

                        byte[] hash = DoubleSHA256(header);

                        // Check if hash meets target (big-endian compare)
                        if (HashMeetsTarget(hash, target))
                        {
                            // Share found!
                            Interlocked.Increment(ref _hashesComputed);
                            // Submit share
                            string submitId = Guid.NewGuid().ToString();
                            var submitReq = new
                            {
                                id = submitId,
                                method = "mining.submit",
                                @params = new object[] { _config.WalletAddress, jobId, extranonce2, nTime, nonce.ToString("x8", CultureInfo.InvariantCulture) }
                            };
                            string submitJson = JsonSerializer.Serialize(submitReq);
                            await writer.WriteLineAsync(submitJson).ConfigureAwait(false);
                            await writer.FlushAsync(ct).ConfigureAwait(false);

                            string? submitResp = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(submitResp))
                            {
                                using var submitDoc = JsonDocument.Parse(submitResp);
                                if (submitDoc.RootElement.TryGetProperty("result", out var res) && res.GetBoolean())
                                {
                                    Interlocked.Increment(ref _acceptedShares);
                                    _logShareSubmitted(_logger, "accepted", null);
                                }
                                else
                                {
                                    Interlocked.Increment(ref _rejectedShares);
                                    _logShareSubmitted(_logger, "rejected", null);
                                }
                            }

                            found = true;
                            break; // stop nonce loop, wait for next work
                        }

                        // Update hashrate periodically
                        if (Interlocked.Increment(ref _hashesComputed) % 10000 == 0)
                        {
                            UpdateHashrate();
                        }
                    }

                    if (!found)
                    {
                        // No nonce found for this job; will continue to next job
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logPoolConnectionFailed(_logger, ex.Message, ex);
                // Wait before reconnecting
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
        }
    }

    private static bool TryParsePoolUrl(string url, out string? host, out int port)
    {
        host = null;
        port = 3333;
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        // Remove stratum+tcp:// prefix
        string trimmed = url;
        if (trimmed.StartsWith("stratum+tcp://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("stratum+tcp://".Length);
        }
        else if (trimmed.StartsWith("stratum://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("stratum://".Length);
        }

        int colon = trimmed.LastIndexOf(':');
        if (colon > 0)
        {
            host = trimmed.AsSpan(0, colon).ToString();
            if (!int.TryParse(trimmed.AsSpan(colon + 1), out port))
            {
                port = 3333;
            }
        }
        else
        {
            host = trimmed;
        }

        return !string.IsNullOrEmpty(host);
    }

    private static string GenerateExtranonce2(int size)
    {
        byte[] bytes = new byte[size];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToUpperInvariant();
    }

    private static byte[] HexToBytes(string hex)
    {
        if (hex.Length % 2 != 0)
        {
            throw new ArgumentException("Invalid hex length.");
        }

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return bytes;
    }

    private static byte[] DoubleSHA256(byte[] data)
    {
        byte[] hash1 = SHA256.HashData(data);
        return SHA256.HashData(hash1);
    }

    private static byte[] ComputeMerkleRoot(byte[] coinbaseHash, string[] branches)
    {
        byte[] current = coinbaseHash;
        foreach (string branchHex in branches)
        {
            byte[] branch = HexToBytes(branchHex);
            byte[] combined = new byte[current.Length + branch.Length];
            Buffer.BlockCopy(current, 0, combined, 0, current.Length);
            Buffer.BlockCopy(branch, 0, combined, current.Length, branch.Length);
            current = DoubleSHA256(combined);
        }
        Array.Reverse(current);
        return current;
    }

    private static uint BitsToTarget(byte[] nBitsBytes)
    {
        if (nBitsBytes.Length != 4)
        {
            return 0;
        }

        byte exp = nBitsBytes[0];
        uint mantissa = (uint)((nBitsBytes[1] << 16) | (nBitsBytes[2] << 8) | nBitsBytes[3]);
        return mantissa << (8 * (exp - 3));
    }

    private static bool HashMeetsTarget(byte[] hash, uint target)
    {
        for (int i = 0; i < 4; i++)
        {
            uint word = (uint)((hash[i * 4] << 24) | (hash[i * 4 + 1] << 16) | (hash[i * 4 + 2] << 8) | hash[i * 4 + 3]);
            if (word < target)
            {
                return true;
            }

            if (word > target)
            {
                return false;
            }
        }

        return false;
    }

    private void UpdateHashrate()
    {
        long now = DateTime.UtcNow.Ticks;
        long elapsed = now - Interlocked.Read(ref _lastHashrateUpdate);
        if (elapsed > TimeSpan.TicksPerSecond)
        {
            long total = Interlocked.Read(ref _hashesComputed);
            double rate = (total - _lastHashrateUpdate) / (elapsed / (double)TimeSpan.TicksPerSecond);
            _hashrate = rate;
            Interlocked.Exchange(ref _lastHashrateUpdate, now);
        }
    }
}
