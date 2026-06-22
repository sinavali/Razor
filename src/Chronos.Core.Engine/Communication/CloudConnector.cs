// -----------------------------------------------------------------------------
// <copyright file="CloudConnector.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Communication;

using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core;
using Core.Exceptions;
using Management.Tasks;
using Services.Update;
using Microsoft.Extensions.Logging;

/// <summary>Default implementation of <see cref="ICloudConnector"/>.</summary>
internal sealed class CloudConnector : ICloudConnector, IAsyncDisposable
{
    private readonly ILogger<CloudConnector> _logger;
    private readonly ISecurityManager _securityManager;
    private readonly IEngineTelemetry _telemetry;
    private readonly IStateManager _stateManager;
    private CancellationTokenSource? _queueCts;
    private Task? _queueProcessingTask;
    private readonly ITaskManager _taskManager;
    private readonly BinaryTransferManager _transferManager;
    private readonly ISelfUpdateManager _selfUpdateManager;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private bool _isConnected;
    private string? _sessionId;
    private int _reconnectAttempt;
    private bool _isDisposing;
    private int _connectionAttemptCounter;

    // For CPU usage calculation
    private DateTime _lastCpuTimeSample = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;

    // LoggerMessage delegates
    private static readonly Action<ILogger, Exception?> _logStartingConnector =
        LoggerMessage.Define(LogLevel.Information, 1, "Starting cloud connector.");

    private static readonly Action<ILogger, Exception?> _logConnectorCancelled =
        LoggerMessage.Define(LogLevel.Information, 3, "Cloud connector cancelled.");

    private static readonly Action<ILogger, string, Exception?> _logErrorInLoop =
        LoggerMessage.Define<string>(LogLevel.Error, 4, "Error in cloud connector loop: {ErrorMessage}");

    private static readonly Action<ILogger, string, Exception?> _logErrorClosingWebSocket =
        LoggerMessage.Define<string>(LogLevel.Warning, 6, "Error closing WebSocket: {ErrorMessage}");

    private static readonly Action<ILogger, Exception?> _logDisconnected =
        LoggerMessage.Define(LogLevel.Information, 7, "Disconnected from cloud.");

    private static readonly Action<ILogger, string, bool, Exception?> _logSentMessage =
        LoggerMessage.Define<string, bool>(LogLevel.Trace, 8, "Sent message: {MessageType} (Encrypted: {Encrypted})");

    private static readonly Action<ILogger, string, Exception?> _logConnectingToEndpoint =
        LoggerMessage.Define<string>(LogLevel.Information, 9, "Connecting to {Endpoint}...");

    private static readonly Action<ILogger, string, Exception?> _logConnectedToEndpoint =
        LoggerMessage.Define<string>(LogLevel.Information, 10, "Connected to {Endpoint}.");

    private static readonly Action<ILogger, Exception?> _logAuthenticating =
        LoggerMessage.Define(LogLevel.Information, 12, "Authenticating with cloud...");

    private static readonly Action<ILogger, string, Exception?> _logAuthenticated =
        LoggerMessage.Define<string>(LogLevel.Information, 13, "Authenticated. Session ID: {SessionId}");

    private static readonly Action<ILogger, Exception?> _logWebSocketClosedByServer =
        LoggerMessage.Define(LogLevel.Information, 14, "WebSocket closed by server.");

    private static readonly Action<ILogger, string, Exception?> _logDeserializationError =
        LoggerMessage.Define<string>(LogLevel.Error, 15, "Error deserializing message: {Json}");

    private static readonly Action<ILogger, string, Exception?> _logReceiveLoopError =
        LoggerMessage.Define<string>(LogLevel.Error, 16, "Error in receive loop: {ErrorMessage}");

    private static readonly Action<ILogger, Exception?> _logReceiveLoopEnded =
        LoggerMessage.Define(LogLevel.Warning, 17, "Receive loop ended.");

    private static readonly Action<ILogger, string, bool, Exception?> _logReceivedMessage =
        LoggerMessage.Define<string, bool>(LogLevel.Trace, 18,
            "Received message: {MessageType} (Encrypted: {Encrypted})");

    private static readonly Action<ILogger, string, Exception?> _logDecryptError =
        LoggerMessage.Define<string>(LogLevel.Error, 19, "Error decrypting message: {ErrorMessage}");

    private static readonly Action<ILogger, string, Exception?> _logAuthFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 20, "Authentication failed: {Error}");

    private static readonly Action<ILogger, Exception?> _logAuthAck =
        LoggerMessage.Define(LogLevel.Information, 21, "Authentication acknowledged.");

    private static readonly Action<ILogger, string, Exception?> _logCommandError =
        LoggerMessage.Define<string>(LogLevel.Error, 23, "Error processing command: {ErrorMessage}");

    private static readonly Action<ILogger, Exception?> _logInvalidCommandPayload =
        LoggerMessage.Define(LogLevel.Warning, 24, "Invalid command payload format.");

    private static readonly Action<ILogger, Exception?> _logStopRequested =
        LoggerMessage.Define(LogLevel.Warning, 25, "Cloud requested engine stop.");

    private static readonly Action<ILogger, Exception?> _logLockBanRequested =
        LoggerMessage.Define(LogLevel.Warning, 26, "Engine is locked/banned. Stopping user tasks.");

    private static readonly Action<ILogger, string, Exception?> _logHeartbeatResponseStatus =
        LoggerMessage.Define<string>(LogLevel.Warning, 27, "Heartbeat response status: {Status}");

    private static readonly Action<ILogger, Exception?> _logAuthInvalid =
        LoggerMessage.Define(LogLevel.Warning, 28, "Authentication invalid. Stopping user tasks.");

    private static readonly Action<ILogger, string, Exception?> _logBinaryTransfer =
        LoggerMessage.Define<string>(LogLevel.Debug, 29, "Binary transfer message received: {MessageType}");

    private static readonly Action<ILogger, Exception?> _logHeartbeatError =
        LoggerMessage.Define(LogLevel.Error, 30, "Error in heartbeat loop.");

    private static readonly Action<ILogger, string, Exception?> _logUnhandledMessageType =
        LoggerMessage.Define<string>(LogLevel.Debug, 31, "Unhandled message type: {MessageType}");

    private static readonly Action<ILogger, Exception?> _logNoCommandHandler =
        LoggerMessage.Define(LogLevel.Warning, 32, "No command handler registered.");

    private static readonly Action<ILogger, int, Exception?> _logConnectingCloudAttempt =
        LoggerMessage.Define<int>(LogLevel.Information, 33, "Connecting to Cloud (attempt {Attempt})...");

    private static readonly Action<ILogger, string, Exception?> _logConnectFailed =
        LoggerMessage.Define<string>(LogLevel.Trace, 34, "Failed to connect to {Endpoint}");

    private static readonly Action<ILogger, string, Exception?> _logBinarySendStarted =
        LoggerMessage.Define<string>(LogLevel.Information, 35, "Started binary send: {FilePath}");

    private static readonly Action<ILogger, string, Exception?> _logBinarySendCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, 36, "Completed binary send: {FilePath}");

    private static readonly Action<ILogger, string, Exception?> _logBinarySendFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 37, "Binary send failed: {FilePath}");

    private static readonly Action<ILogger, string, string, Exception?> _logBinaryTransferSaved =
        LoggerMessage.Define<string, string>(LogLevel.Information, 38, "Binary transfer {TransferId} saved to {Path}");

    private static readonly Action<ILogger, string, Exception?> _logBinaryTransferNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning, 39, "Binary transfer {TransferId} completed but transfer not found.");

    private static readonly Action<ILogger, string, string, Exception?> _logBinaryTransferEnded =
        LoggerMessage.Define<string, string>(LogLevel.Warning, 40, "Binary transfer {TransferId} ended with status: {Status}");

    private static readonly Action<ILogger, string, string, Exception?> _logBinaryTransferAck =
        LoggerMessage.Define<string, string>(LogLevel.Debug, 41, "Binary transfer ACK for {TransferId}: {Status}");

    private static readonly Action<ILogger, Exception?> _logSelfUpdateInstallFailed =
        LoggerMessage.Define(LogLevel.Error, 42, "Self-update installation failed.");

    private static readonly Action<ILogger, Exception?> _logSelfUpdateMetadataFailed =
        LoggerMessage.Define(LogLevel.Error, 43, "Failed to process self-update metadata.");

    private static readonly Action<ILogger, string, Exception?> _logQueuedMessage =
    LoggerMessage.Define<string>(LogLevel.Information, 44, "Message of type {MessageType} queued for later delivery.");

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public string? SessionId => _sessionId;

    /// <summary>Event raised when a command is received.</summary>
    public event Func<CloudCommand, Task>? CommandReceived;

    /// <summary>Initialises a new instance of the <see cref="CloudConnector"/> class.</summary>
    public CloudConnector(
        ILogger<CloudConnector> logger,
        ISecurityManager securityManager,
        IEngineTelemetry telemetry,
        IStateManager stateManager,
        ITaskManager taskManager,
        BinaryTransferManager transferManager,
        ISelfUpdateManager selfUpdateManager)
    {
        _logger = logger;
        _securityManager = securityManager;
        _telemetry = telemetry;
        _stateManager = stateManager;
        _taskManager = taskManager;
        _transferManager = transferManager;
        _selfUpdateManager = selfUpdateManager;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logStartingConnector(_logger, null);
        Console.WriteLine("Cloud connector starting.");

        while (true)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Credentials.EnsureAvailable();

                await this.ConnectAndAuthenticateAsync(cancellationToken).ConfigureAwait(false);

                if (_isConnected)
                {
                    // Keep the connection alive; wait indefinitely but respect cancellation.
                    try
                    {
                        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logConnectorCancelled(_logger, null);
                Console.WriteLine("Cloud connector cancelled.");
                break;
            }
            catch (Exception ex)
            {
                _logErrorInLoop(_logger, ex.Message, ex);
                Console.WriteLine($"Error in connector loop: {ex.Message}");
                await Task.Delay(this.GetReconnectDelay(), cancellationToken).ConfigureAwait(false);
            }
        }

        await this.DisconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_isDisposing)
        {
            return;
        }

        // Cancel queue processing
        if (_queueCts != null)
        {
            await _queueCts.CancelAsync().ConfigureAwait(false);
            if (_queueProcessingTask != null)
            {
                try { await _queueProcessingTask.ConfigureAwait(false); } catch { }
            }
            _queueCts.Dispose();
            _queueCts = null;
            _queueProcessingTask = null;
        }

        _isConnected = false;
        if (_receiveCts != null)
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);
        }

        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logErrorClosingWebSocket(_logger, ex.Message, ex);
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        _telemetry.SetConnectionState(false);
        _logDisconnected(_logger, null);
        Console.WriteLine("Disconnected from cloud.");
    }

    /// <inheritdoc/>
    public async Task SendAsync(CloudMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var ws = _webSocket;
        bool isOpen = ws != null && ws.State == WebSocketState.Open;

        // If socket is not open, enqueue non‑critical messages
        if (!isOpen)
        {
            if (message.MessageType != "Heartbeat" &&
                message.MessageType != "Auth" &&
                message.MessageType != "AuthConfirm")
            {
                string json = JsonSerializer.Serialize(message);
                await _stateManager.EnqueueOutgoingMessageAsync(message.MessageType, json, cancellationToken)
                    .ConfigureAwait(false);
                _logQueuedMessage(_logger, message.MessageType, null);
                return;
            }

            // Critical messages require an open connection
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        // At this point, ws is guaranteed non‑null and open
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            message.Version = AppConstants.EngineVersion;

            if (message.Encrypted)
            {
                string jsonPayload = JsonSerializer.Serialize(message.Payload);
                string encryptedPayload = _securityManager.EncryptMessage(jsonPayload);
                message.Payload = encryptedPayload;
            }

            string json = JsonSerializer.Serialize(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await ws!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
            _logSentMessage(_logger, message.MessageType, message.Encrypted, null);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SendBinaryAsync(string filePath, string contentType, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        _logBinarySendStarted(_logger, filePath, null);

        var fileInfo = new FileInfo(filePath);
        long totalSize = fileInfo.Length;
        string transferId = Guid.NewGuid().ToString();
        string checksum;

        // Compute checksum using static HashData
        using (var stream = File.OpenRead(filePath))
        {
            checksum = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        }

        // Start transfer
        var startPayload = new
        {
            TransferId = transferId,
            FileName = Path.GetFileName(filePath),
            TotalSize = totalSize,
            ContentType = contentType,
            Checksum = checksum
        };

        var startMessage = new CloudMessage
        {
            MessageType = "BinaryTransferStart",
            Encrypted = true,
            Payload = startPayload
        };

        await this.SendAsync(startMessage, cancellationToken).ConfigureAwait(false);

        // Register outgoing transfer
        _transferManager.StartOutgoing(transferId, filePath, contentType, totalSize, checksum);

        int chunkSize = AppConstants.DefaultChunkSize;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var (offset, data) = _transferManager.GetNextChunk(transferId, chunkSize);
                if (data == null)
                {
                    // End of file
                    break;
                }

                var chunkPayload = new
                {
                    TransferId = transferId,
                    Offset = offset,
                    Data = Convert.ToBase64String(data)
                };

                var chunkMessage = new CloudMessage
                {
                    MessageType = "BinaryChunk",
                    Encrypted = true,
                    Payload = chunkPayload
                };

                await this.SendAsync(chunkMessage, cancellationToken).ConfigureAwait(false);

                // Small delay to avoid flooding
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            // Send end message
            var endPayload = new
            {
                TransferId = transferId,
                Status = "Success"
            };

            var endMessage = new CloudMessage
            {
                MessageType = "BinaryTransferEnd",
                Encrypted = true,
                Payload = endPayload
            };

            await this.SendAsync(endMessage, cancellationToken).ConfigureAwait(false);
            _transferManager.CompleteOutgoing(transferId);
            _logBinarySendCompleted(_logger, filePath, null);
        }
        catch (Exception ex)
        {
            _transferManager.CancelOutgoing(transferId);
            _logBinarySendFailed(_logger, filePath, ex);
            throw;
        }
    }

    private async Task ConnectAndAuthenticateAsync(CancellationToken cancellationToken)
    {
        _connectionAttemptCounter++;

        if (RuntimeEnvironment.IsProduction)
        {
            _logConnectingCloudAttempt(_logger, _connectionAttemptCounter, null);
            try
            {
                await ConnectSingleEndpointAsync(AppConstants.PrimaryEndpoint, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception)
            {
                // Silently fallback.
            }

            try
            {
                await ConnectSingleEndpointAsync(AppConstants.FallbackEndpoint, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                throw new EngineException("Failed to connect to any cloud endpoint.", ex);
            }
        }
        else
        {
            string[] endpoints = { AppConstants.PrimaryEndpoint, AppConstants.FallbackEndpoint };
            Exception? lastException = null;
            foreach (string endpoint in endpoints)
            {
                try
                {
                    _logConnectingToEndpoint(_logger, endpoint, null);
                    Console.WriteLine($"Connecting to {endpoint}...");
                    await this.ConnectSingleEndpointAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logConnectFailed(_logger, endpoint, ex);
                }
            }

            throw new EngineException("Failed to connect to any cloud endpoint.", lastException!);
        }
    }

    private async Task ConnectSingleEndpointAsync(string endpoint, CancellationToken cancellationToken)
    {
        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(new Uri(endpoint), cancellationToken).ConfigureAwait(false);

        if (_webSocket.State == WebSocketState.Open)
        {
            if (RuntimeEnvironment.IsDevelopment)
            {
                _logConnectedToEndpoint(_logger, endpoint, null);
                Console.WriteLine($"Connected to {endpoint}.");
            }
            else
            {
                _logConnectedToEndpoint(_logger, "Cloud", null);
                Console.WriteLine("Connected to Cloud.");
            }

            await this.AuthenticateAsync(cancellationToken).ConfigureAwait(false);

            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Task.Run(() => this.ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);

            _isConnected = true;
            _queueCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _queueProcessingTask = Task.Run(
                () => ProcessOutgoingQueueAsync(_queueCts.Token),
                _queueCts.Token);

            _telemetry.SetConnectionState(true);
            _reconnectAttempt = 0;

            await this.SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            _ = Task.Run(() => this.HeartbeatLoopAsync(cancellationToken), cancellationToken);
        }
    }

    private async Task ProcessOutgoingQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_isConnected && _webSocket?.State == WebSocketState.Open)
                {
                    var pending = await _stateManager.GetPendingOutgoingMessagesAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var msg in pending)
                    {
                        if (!_isConnected)
                        {
                            break;
                        }
                        // Deserialize message from JSON
                        var cloudMsg = JsonSerializer.Deserialize<CloudMessage>(msg.PayloadJson);
                        if (cloudMsg == null)
                        {
                            continue;
                        }
                        // Send it
                        try
                        {
                            await SendAsync(cloudMsg, cancellationToken).ConfigureAwait(false);
                            // Mark as sent
                            await _stateManager.DeleteOutgoingMessageAsync(msg.Id, cancellationToken).ConfigureAwait(false);
                        }
                        catch
                        {
                            // If send fails, break and retry later
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                // Log and continue
            }
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        _logAuthenticating(_logger, null);
        Console.WriteLine("Authenticating with cloud...");

        var authMessage = new CloudMessage
        {
            MessageType = "Auth",
            Encrypted = false,
            Payload = new
            {
                Credentials.Username,
                Credentials.Password,
                InstanceApiKey = Credentials.InstanceApiKey,
                EngineVersion = AppConstants.EngineVersion,
                ClientCapabilities = AppConstants.SupportedCapabilities,
                PublicKey = _securityManager.GetPublicKey()
            }
        };

        await this.SendAsync(authMessage, cancellationToken).ConfigureAwait(false);

        bool authResponseReceived = false;
        while (!authResponseReceived && !cancellationToken.IsCancellationRequested)
        {
            if (!string.IsNullOrEmpty(_sessionId))
            {
                authResponseReceived = true;
                break;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrEmpty(_sessionId))
        {
            throw new EngineException("Authentication failed: no session ID received.");
        }

        _logAuthenticated(_logger, _sessionId, null);
        Console.WriteLine($"Authenticated. Session ID: {_sessionId}");
        await _stateManager.SetSessionIdAsync(_sessionId, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        StringBuilder messageBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested && _webSocket != null &&
               _webSocket.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result = await _webSocket
                    .ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logWebSocketClosedByServer(_logger, null);
                    Console.WriteLine("WebSocket closed by server.");
                    _isConnected = false;
                    break;
                }

                string messagePart = Encoding.UTF8.GetString(buffer, 0, result.Count);
                messageBuilder.Append(messagePart);

                if (result.EndOfMessage)
                {
                    string json = messageBuilder.ToString();
                    messageBuilder.Clear();

                    try
                    {
                        CloudMessage? message = JsonSerializer.Deserialize<CloudMessage>(json);
                        if (message != null)
                        {
                            await this.ProcessReceivedMessageAsync(message, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logDeserializationError(_logger, json, ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logReceiveLoopError(_logger, ex.Message, ex);
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }

        _isConnected = false;
        _telemetry.SetConnectionState(false);
        _logReceiveLoopEnded(_logger, null);
        Console.WriteLine("Receive loop ended.");
    }

    private async Task ProcessReceivedMessageAsync(CloudMessage message, CancellationToken cancellationToken)
    {
        _logReceivedMessage(_logger, message.MessageType, message.Encrypted, null);

        if (message.Encrypted && message.Payload is string encryptedPayload)
        {
            try
            {
                string decryptedJson = _securityManager.DecryptMessage(encryptedPayload);
                message.Payload = JsonSerializer.Deserialize<object>(decryptedJson);
            }
            catch (Exception ex)
            {
                _logDecryptError(_logger, ex.Message, ex);
                return;
            }
        }

        switch (message.MessageType)
        {
            case "AuthResponse":
                if (message.Payload is Dictionary<string, object> payload)
                {
                    if (payload.TryGetValue("Status", out object? statusObj) && statusObj.ToString() == "Success")
                    {
                        _sessionId = payload.TryGetValue("SessionId", out object? sessionObj)
                            ? sessionObj.ToString()
                            : null;
                        if (payload.TryGetValue("PublicKey", out object? pubKeyObj))
                        {
                            _securityManager.SetCloudPublicKey(pubKeyObj.ToString()!);
                        }

                        if (payload.TryGetValue("Nonce", out object? nonceObj))
                        {
                            _securityManager.SetNonce(nonceObj.ToString()!);
                        }

                        _securityManager.DeriveSharedSecret();

                        if (_sessionId != null)
                        {
                            string challenge = _securityManager.GenerateChallenge(nonceObj?.ToString() ?? string.Empty);
                            var confirm = new CloudMessage
                            {
                                MessageType = "AuthConfirm",
                                Encrypted = true,
                                Payload = new { Challenge = challenge }
                            };
                            await this.SendAsync(confirm, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        string error = payload.TryGetValue("ErrorMessage", out object? errObj)
                            ? errObj.ToString() ?? "Unknown error"
                            : "Unknown error";
                        _logAuthFailed(_logger, error, null);
                        throw new EngineException($"Authentication failed: {error}");
                    }
                }

                break;

            case "AuthAck":
                _logAuthAck(_logger, null);
                Console.WriteLine("Authentication acknowledged.");
                break;

            case "HeartbeatResponse":
                await this.HandleHeartbeatResponseAsync(message, cancellationToken).ConfigureAwait(false);
                break;

            case "Command":
                if (message.Payload is Dictionary<string, object> payloadDict)
                {
                    try
                    {
                        var command = new CloudCommand
                        {
                            CommandId = Convert.ToInt32(payloadDict.GetValueOrDefault("CommandId", 0),
                                CultureInfo.InvariantCulture),
                            CommandType = payloadDict.GetValueOrDefault("CommandType")?.ToString() ?? string.Empty,
                            Parameters = payloadDict.GetValueOrDefault("Parameters"),
                            TimeoutSeconds = payloadDict.GetValueOrDefault("TimeoutSeconds") as int?,
                            CorrelationId = message.CorrelationId
                        };

                        if (this.CommandReceived != null)
                        {
                            await this.CommandReceived.Invoke(command).ConfigureAwait(false);
                        }
                        else
                        {
                            _logNoCommandHandler(_logger, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logCommandError(_logger, ex.Message, ex);
                    }
                }
                else
                {
                    _logInvalidCommandPayload(_logger, null);
                }

                break;

            case "CommandResponse":
                break;

            case "BinaryTransferStart":
            case "BinaryChunk":
            case "BinaryTransferEnd":
            case "BinaryTransferAck":
                await this.ProcessBinaryTransferAsync(message, cancellationToken).ConfigureAwait(false);
                break;

            case "BroadcastMessage":
                await this.DisplayBroadcastMessageAsync(message, cancellationToken).ConfigureAwait(false);
                break;

            default:
                _logUnhandledMessageType(_logger, message.MessageType, null);
                break;
        }
    }

    private async Task ProcessBinaryTransferAsync(CloudMessage message, CancellationToken cancellationToken)
    {
        _logBinaryTransfer(_logger, message.MessageType, null);

        if (message.Payload is not Dictionary<string, object> payload)
        {
            return;
        }

        switch (message.MessageType)
        {
            case "BinaryTransferStart":
                string transferId = payload.GetValueOrDefault("TransferId")?.ToString() ?? string.Empty;
                string fileName = payload.GetValueOrDefault("FileName")?.ToString() ?? "unknown";
                long totalSize = Convert.ToInt64(payload.GetValueOrDefault("TotalSize", 0L), CultureInfo.InvariantCulture);
                string checksum = payload.GetValueOrDefault("Checksum")?.ToString() ?? string.Empty;
                string contentType = payload.GetValueOrDefault("ContentType")?.ToString() ?? "application/octet-stream";

                _transferManager.StartIncoming(transferId, fileName, totalSize, checksum, contentType);
                break;

            case "BinaryChunk":
                string chunkTransferId = payload.GetValueOrDefault("TransferId")?.ToString() ?? string.Empty;
                long offset = Convert.ToInt64(payload.GetValueOrDefault("Offset", 0L), CultureInfo.InvariantCulture);
                string dataB64 = payload.GetValueOrDefault("Data")?.ToString() ?? string.Empty;
                byte[] chunkData = Convert.FromBase64String(dataB64);

                bool complete = _transferManager.AppendChunk(chunkTransferId, offset, chunkData);
                if (complete)
                {
                    // Send ACK
                    var ackPayload = new
                    {
                        TransferId = chunkTransferId,
                        Status = "Success"
                    };
                    var ackMessage = new CloudMessage
                    {
                        MessageType = "BinaryTransferAck",
                        Encrypted = true,
                        Payload = ackPayload
                    };
                    await this.SendAsync(ackMessage, cancellationToken).ConfigureAwait(false);
                    _logBinaryTransfer(_logger, "BinaryTransferAck", null);
                }
                break;

            case "BinaryTransferEnd":
                string endTransferId = payload.GetValueOrDefault("TransferId")?.ToString() ?? string.Empty;
                string status = payload.GetValueOrDefault("Status")?.ToString() ?? "Success";

                if (status == "Success")
                {
                    var transfer = _transferManager.GetCompletedTransfer(endTransferId);
                    if (transfer != null)
                    {
                        // Save the received file to disk
                        string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "downloads");
                        Directory.CreateDirectory(outputDir);
                        string outputPath = Path.Combine(outputDir, transfer.FileName);
                        await File.WriteAllBytesAsync(outputPath, transfer.Data, cancellationToken).ConfigureAwait(false);
                        _logBinaryTransferSaved(_logger, endTransferId, outputPath, null);
                    }
                    else
                    {
                        _logBinaryTransferNotFound(_logger, endTransferId, null);
                    }
                }
                else
                {
                    _transferManager.CancelIncoming(endTransferId);
                    _logBinaryTransferEnded(_logger, endTransferId, status, null);
                }
                break;

            case "BinaryTransferAck":
                string ackTransferId = payload.GetValueOrDefault("TransferId")?.ToString() ?? string.Empty;
                string ackStatus = payload.GetValueOrDefault("Status")?.ToString() ?? "Success";
                _logBinaryTransferAck(_logger, ackTransferId, ackStatus, null);
                break;
        }
    }

    private async Task HandleHeartbeatResponseAsync(CloudMessage message, CancellationToken cancellationToken)
    {
        if (message.Payload is not Dictionary<string, object> payload)
        {
            return;
        }

        if (payload.TryGetValue("Status", out object? statusObj))
        {
            string? status = statusObj.ToString();
            if (status == "Stop")
            {
                _logStopRequested(_logger, null);
                Console.WriteLine("Cloud requested engine stop.");
                cancellationToken = new CancellationToken(true);
                return;
            }
            else if (status == "Lock" || status == "Ban")
            {
                _logLockBanRequested(_logger, null);
                Console.WriteLine("🔴 Engine is locked/banned. Stopping user tasks.");
                var stopCommand = new CloudCommand
                {
                    CommandId = 2001, // EmergencyStop
                    CommandType = "EmergencyStop",
                    CorrelationId = Guid.NewGuid().ToString()
                };
                if (this.CommandReceived != null)
                {
                    await this.CommandReceived.Invoke(stopCommand).ConfigureAwait(false);
                }
            }
            else
            {
                _logHeartbeatResponseStatus(_logger, status ?? "unknown", null);
            }
        }

        if (payload.TryGetValue("Commands", out object? commandsObj) && commandsObj is object[] commands)
        {
            foreach (object cmd in commands)
            {
                if (cmd is Dictionary<string, object> cmdDict)
                {
                    var command = new CloudCommand
                    {
                        CommandId = Convert.ToInt32(cmdDict.GetValueOrDefault("CommandId", 0),
                            CultureInfo.InvariantCulture),
                        CommandType = cmdDict.GetValueOrDefault("CommandType")?.ToString() ?? string.Empty,
                        Parameters = cmdDict.GetValueOrDefault("Parameters"),
                        CorrelationId = Guid.NewGuid().ToString()
                    };
                    if (this.CommandReceived != null)
                    {
                        _ = Task.Run(() => this.CommandReceived.Invoke(command), cancellationToken);
                    }
                }
            }
        }

        if (payload.TryGetValue("AuthValid", out object? authValidObj) && authValidObj is bool authValid && !authValid)
        {
            _logAuthInvalid(_logger, null);
            Console.WriteLine("🔴 Authentication invalid. Stopping user tasks.");
            var stopCommand = new CloudCommand
            {
                CommandId = 2001,
                CommandType = "EmergencyStop",
                CorrelationId = Guid.NewGuid().ToString()
            };
            if (this.CommandReceived != null)
            {
                await this.CommandReceived.Invoke(stopCommand).ConfigureAwait(false);
            }
        }

        if (payload.TryGetValue("AdminMessage", out object? adminMsgObj) && adminMsgObj.ToString() is string adminMsg)
        {
            Console.WriteLine($"\n[ADMIN] {adminMsg}");
        }

        // Check for self-update metadata
        if (payload.TryGetValue("NewVersion", out object? newVersionObj) && newVersionObj is string newVersion &&
            payload.TryGetValue("DownloadUrl", out object? downloadUrlObj) && downloadUrlObj is string downloadUrlStr &&
            payload.TryGetValue("Checksum", out object? checksumObj) && checksumObj is string checksum)
        {
            try
            {
                _selfUpdateManager.CheckForUpdate(newVersion, new Uri(downloadUrlStr), checksum);
                if (_selfUpdateManager.IsUpdateAvailable)
                {
                    // Trigger update in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _selfUpdateManager.InstallUpdateAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logSelfUpdateInstallFailed(_logger, ex);
                        }
                    }, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logSelfUpdateMetadataFailed(_logger, ex);
            }
        }
    }

    private async Task DisplayBroadcastMessageAsync(CloudMessage message, CancellationToken cancellationToken)
    {
        if (message.Payload is not Dictionary<string, object> payload)
        {
            return;
        }

        string text = payload.GetValueOrDefault("Text")?.ToString() ?? string.Empty;
        string style = payload.GetValueOrDefault("Style")?.ToString() ?? "info";

        Console.ForegroundColor = style switch
        {
            "error" => ConsoleColor.Red,
            "warning" => ConsoleColor.Yellow,
            "success" => ConsoleColor.Green,
            _ => ConsoleColor.Cyan
        };
        Console.WriteLine($"\n=== BROADCAST ===\n{text}\n================\n");
        Console.ResetColor();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isConnected)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(AppConstants.DefaultHeartbeatIntervalSeconds), cancellationToken)
                    .ConfigureAwait(false);
                await this.SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logHeartbeatError(_logger, ex);
            }
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (!_isConnected || _webSocket == null)
        {
            return;
        }

        // Collect system metrics
        double cpuUsage = GetCpuUsage();
        long memoryUsage = Process.GetCurrentProcess().WorkingSet64;
        int tasksRunning = _taskManager.RunningTasks.Count;
        long? liveTickAgeTicks = GetLiveTickAgeTicks();

        // Record live tick age telemetry
        if (liveTickAgeTicks.HasValue)
        {
            _telemetry.RecordLiveTickAge(liveTickAgeTicks.Value);
        }

        var heartbeat = new CloudMessage
        {
            MessageType = "Heartbeat",
            Encrypted = true,
            Payload = new
            {
                EngineId = _stateManager.EngineId,
                LocalTimestamp = DateTime.UtcNow,
                IsConnected = _isConnected,
                Health = new
                {
                    CpuUsage = cpuUsage,
                    MemoryUsage = memoryUsage,
                    TasksRunning = tasksRunning,
                    LiveTickAge = liveTickAgeTicks ?? 0
                }
            }
        };

        await this.SendAsync(heartbeat, cancellationToken).ConfigureAwait(false);
    }

    private double GetCpuUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var totalProcessorTime = process.TotalProcessorTime;
            var deltaTime = (now - _lastCpuTimeSample).TotalSeconds;
            var deltaProcessorTime = (totalProcessorTime - _lastTotalProcessorTime).TotalSeconds;

            _lastCpuTimeSample = now;
            _lastTotalProcessorTime = totalProcessorTime;

            if (deltaTime <= 0 || deltaProcessorTime <= 0)
            {
                return 0.0;
            }

            // CPU usage as percentage of total available (Environment.ProcessorCount)
            double usage = (deltaProcessorTime / deltaTime) / Environment.ProcessorCount * 100.0;
            return Math.Min(100.0, Math.Max(0.0, usage));
        }
        catch
        {
            return 0.0;
        }
    }

    private long? GetLiveTickAgeTicks()
    {
        var lastTick = _taskManager.GetLastLiveTickTimestamp();
        if (lastTick.HasValue)
        {
            return (DateTime.UtcNow - lastTick.Value).Ticks;
        }
        return null;
    }

    private TimeSpan GetReconnectDelay()
    {
        _reconnectAttempt++;
        double delay = Math.Min(AppConstants.MaxRetryDelaySeconds, AppConstants.DefaultRetryDelaySeconds * Math.Pow(1.5, _reconnectAttempt));
        return TimeSpan.FromSeconds(delay);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposing)
        {
            return;
        }

        _isDisposing = true;

        await this.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _receiveCts?.Dispose();
        _sendLock.Dispose();
        _transferManager.Dispose();
        _queueCts?.Dispose();
    }
}
