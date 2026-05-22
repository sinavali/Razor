using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Chronos.Abstractions.Adapters;
using Chronos.Abstractions.Shared;

namespace Chronos.Samples.Plugins.Adapters.MT5;

/// <summary>
/// TCP client that connects to the MT5 bridge EA.
/// Uses a single reader loop with request/response demultiplexing.
/// </summary>
internal sealed class Mt5BridgeClient : IDisposable, IAsyncDisposable
{
    private readonly Mt5AdapterConfiguration _config;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _lifecycleCts;
    private Task? _readerTask;
    private bool _disposed;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event Action<string, Tick>? OnTick;
    public event Action<ExecutionReport>? OnExecutionUpdate;
    public bool IsConnected { get; private set; }

    public Mt5BridgeClient(Mt5AdapterConfiguration config) => _config = config;

    public async Task ConnectAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _tcpClient = new TcpClient
            { NoDelay = true, ReceiveTimeout = _config.TimeoutMs, SendTimeout = _config.TimeoutMs };
        await _tcpClient.ConnectAsync(_config.BridgeHost, _config.BridgePort, ct).ConfigureAwait(false);
        _stream = _tcpClient.GetStream();
        _lifecycleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readerTask = Task.Run(() => ReaderLoopAsync(_lifecycleCts.Token), _lifecycleCts.Token);
        IsConnected = true;
    }

    public async Task<TResponse?> SendCommandAsync<TResponse>(string command, object payload, CancellationToken ct)
    {
        string requestId = Guid.NewGuid().ToString("N");
        var envelope = new { requestId, command, payload };
        string json = Mt5MessageProtocol.Serialize(envelope);

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await Mt5MessageProtocol.WriteMessageAsync(_stream!, json, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_config.TimeoutMs);
        try
        {
            string responseJson = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            var response = Mt5MessageProtocol.Deserialize<ResponseEnvelope<TResponse>>(responseJson);
            return response is not null ? response.Payload : default;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"Command '{command}' timed out after {_config.TimeoutMs}ms.");
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    private async Task ReaderLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _stream is not null)
        {
            try
            {
                string json = await Mt5MessageProtocol.ReadMessageAsync(_stream, ct).ConfigureAwait(false);
                var envelope = Mt5MessageProtocol.Deserialize<RawEnvelope>(json);
                if (envelope is null) continue;

                if (envelope.RequestId is not null && _pendingRequests.TryRemove(envelope.RequestId, out var tcs))
                {
                    tcs.TrySetResult(json);
                }
                else
                {
                    DispatchEvent(envelope.Command, json);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException) when (ct.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031
            catch (Exception)
#pragma warning restore CA1031
            {
                break;
            }
        }

        IsConnected = false;
    }

    private void DispatchEvent(string? command, string json)
    {
        if (command == "tick")
        {
            var msg = Mt5MessageProtocol.Deserialize<TickEventMessage>(json);
            if (msg?.Payload is not null)
            {
                var dto = msg.Payload.Tick;
                long netTicks = TimeHelpers.FromUnixSeconds(dto.Time);   // <-- fix here
                var tick = new Tick(netTicks, dto.Bid, dto.Ask, dto.Volume, dto.IsSynthetic);
                OnTick?.Invoke(msg.Payload.Symbol, tick);
            }
        }
        else if (command == "execution")
        {
            var msg = Mt5MessageProtocol.Deserialize<ExecutionReportMessage>(json);
            if (msg is not null) OnExecutionUpdate?.Invoke(msg.Report);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_lifecycleCts is not null)
            await _lifecycleCts.CancelAsync().ConfigureAwait(false);
        if (_readerTask is not null)
            await _readerTask.ConfigureAwait(false);
        await CleanupResourcesAsync().ConfigureAwait(false);
    }

    private async Task CleanupResourcesAsync()
    {
        if (_stream is not null)
            await _stream.DisposeAsync().ConfigureAwait(false);
#pragma warning disable CA1849
        _tcpClient?.Dispose();
        _lifecycleCts?.Dispose();
        _lifecycleCts = null;
#pragma warning restore CA1849
        IsConnected = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifecycleCts?.Cancel();
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _sendLock.Dispose();
        _lifecycleCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_lifecycleCts is not null)
            await _lifecycleCts.CancelAsync().ConfigureAwait(false);
        if (_readerTask is not null)
            await _readerTask.ConfigureAwait(false);
        await CleanupResourcesAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }

    // DTOs – instantiated via JSON deserialisation, suppress CA1812.
#pragma warning disable CA1812
    private sealed record RawEnvelope(string? RequestId, string Command);

    private sealed record ResponseEnvelope<T>(string RequestId, string Command, T? Payload);

    private sealed record TickEventMessage(TickEventPayload? Payload);

    private sealed record TickEventPayload(string Symbol, TickDto Tick);

    private sealed record TickDto(long Time, double Bid, double Ask, double Volume, bool IsSynthetic);

    private sealed record ExecutionReportMessage(ExecutionReport Report);
#pragma warning restore CA1812
}