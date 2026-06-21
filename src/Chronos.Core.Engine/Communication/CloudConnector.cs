// -----------------------------------------------------------------------------
// <copyright file="CloudConnector.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Communication;

using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Core.Exceptions;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages the WebSocket connection to the cloud with infinite retry on failure.
/// </summary>
internal interface ICloudConnector
{
    /// <summary>Gets a value indicating whether the connection is established.</summary>
    bool IsConnected { get; }

    /// <summary>Gets the current session identifier.</summary>
    string? SessionId { get; }

    /// <summary>Runs the main connection loop with infinite retry.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RunAsync(CancellationToken cancellationToken);

    /// <summary>Disconnects gracefully.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>Sends a message to the cloud.</summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendAsync(CloudMessage message, CancellationToken cancellationToken);
}

/// <summary>Default implementation of <see cref="ICloudConnector"/>.</summary>
internal sealed class CloudConnector : ICloudConnector, IAsyncDisposable
{
    private readonly ILogger<CloudConnector> _logger;
    private readonly ISecurityManager _securityManager;
    private readonly IEngineTelemetry _telemetry;
    private readonly IStateManager _stateManager;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private bool _isConnected;
    private string? _sessionId;
    private int _reconnectAttempt;
    private bool _isDisposing;
    private int _connectionAttemptCounter;

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

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public string? SessionId => _sessionId;

    /// <summary>Event raised when a command is received.</summary>
    public event Func<CloudCommand, Task>? CommandReceived;

    /// <summary>Initialises a new instance of the <see cref="CloudConnector"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="securityManager">Security manager.</param>
    /// <param name="telemetry">Telemetry service.</param>
    /// <param name="stateManager">State manager.</param>
    public CloudConnector(
        ILogger<CloudConnector> logger,
        ISecurityManager securityManager,
        IEngineTelemetry telemetry,
        IStateManager stateManager)
    {
        _logger = logger;
        _securityManager = securityManager;
        _telemetry = telemetry;
        _stateManager = stateManager;
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

                // Ensure credentials are available (prompt if needed).
                Credentials.EnsureAvailable();

                await this.ConnectAndAuthenticateAsync(cancellationToken).ConfigureAwait(false);

                if (_isConnected)
                {
                    await this.ProcessMessagesAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isConnected)
        {
            try
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_isDisposing)
        {
            return;
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

        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

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
            await _webSocket
                .SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
            _logSentMessage(_logger, message.MessageType, message.Encrypted, null);
        }
        finally
        {
            _sendLock.Release();
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
                await this.ConnectSingleEndpointAsync(AppConstants.PrimaryEndpoint, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception)
            {
                // Silently fallback.
            }

            try
            {
                await this.ConnectSingleEndpointAsync(AppConstants.FallbackEndpoint, cancellationToken).ConfigureAwait(false);
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
            _telemetry.SetConnectionState(true);
            _reconnectAttempt = 0;

            await this.SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            _ = Task.Run(() => this.HeartbeatLoopAsync(cancellationToken), cancellationToken);
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
                    if (payload.TryGetValue("Status", out object? statusObj) && statusObj?.ToString() == "Success")
                    {
                        _sessionId = payload.TryGetValue("SessionId", out object? sessionObj)
                            ? sessionObj?.ToString()
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
                            ? errObj?.ToString() ?? "Unknown error"
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

    private async Task HandleHeartbeatResponseAsync(CloudMessage message, CancellationToken cancellationToken)
    {
        if (message.Payload is not Dictionary<string, object> payload)
        {
            return;
        }

        if (payload.TryGetValue("Status", out object? statusObj))
        {
            string? status = statusObj?.ToString();
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

        if (payload.TryGetValue("AdminMessage", out object? adminMsgObj) && adminMsgObj?.ToString() is string adminMsg)
        {
            Console.WriteLine($"\n[ADMIN] {adminMsg}");
        }
    }

    private async Task ProcessBinaryTransferAsync(CloudMessage message, CancellationToken cancellationToken)
    {
        _logBinaryTransfer(_logger, message.MessageType, null);
        await Task.CompletedTask.ConfigureAwait(false);
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
                    CpuUsage = 0.0,
                    MemoryUsage = 0L,
                    TasksRunning = 0,
                    LiveTickAge = 0.0
                }
            }
        };

        await this.SendAsync(heartbeat, cancellationToken).ConfigureAwait(false);
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
    }
}
