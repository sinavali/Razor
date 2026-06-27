// -----------------------------------------------------------------------------
// <copyright file="EngineHostedService.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Core;

using Chronos.Core.Engine.Core.Exceptions;
using Communication;
using Extensions;
using Kernel;
using Management.Commands;
using Management.Scheduling;
using Management.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Services.BehaviorRecorder;
using Services.Mining;
using Services.Update;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Hosted service that runs the Chronos Engine in the background.
/// </summary>
[SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
    Justification = "Console output for CLI; no localization required.")]
internal sealed class EngineHostedService : IHostedService, IDisposable
{
    private readonly ILogger<EngineHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly CancellationTokenSource _shutdownCts = new();
    private Task? _engineTask;
    private bool _disposed;

    // LoggerMessage delegates for performance (CA1848)
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> _logStartingService =
        LoggerMessage.Define(LogLevel.Information, 1, "Starting Chronos Engine as a service...");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> _logStoppingService =
        LoggerMessage.Define(LogLevel.Information, 2, "Stopping Chronos Engine service...");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, Exception?> _logStoppedService =
        LoggerMessage.Define(LogLevel.Information, 3, "Chronos Engine service stopped.");

    public EngineHostedService(ILogger<EngineHostedService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Starts the engine as a background service.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logStartingService(_logger, null);
        _engineTask = Task.Run(() => RunEngineAsync(_shutdownCts.Token), _shutdownCts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the engine gracefully.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logStoppingService(_logger, null);
        await _shutdownCts.CancelAsync().ConfigureAwait(false);
        if (_engineTask != null)
        {
            try
            {
                await _engineTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* expected */ }
        }
        _logStoppedService(_logger, null);
    }

    private async Task RunEngineAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sp = scope.ServiceProvider;

            // Resolve core services
            var loggingService = sp.GetRequiredService<ILoggingService>();
            await loggingService.InitializeAsync(cancellationToken).ConfigureAwait(false);

            var telemetry = sp.GetRequiredService<IEngineTelemetry>();
            telemetry.RecordStartup();

            var stateManager = sp.GetRequiredService<IStateManager>();
            await stateManager.LoadStateAsync(cancellationToken).ConfigureAwait(false);

            // Restore live state
            var liveState = await stateManager.LoadLiveStateAsync(cancellationToken).ConfigureAwait(false);
            if (liveState != null)
            {
                var taskManager = sp.GetRequiredService<ITaskManager>() as TaskManager;
                if (taskManager != null)
                {
                    string? restoredId = await taskManager.RestoreLiveTaskAsync(liveState, cancellationToken)
                        .ConfigureAwait(false);
                    if (restoredId != null)
                    {
                        Log.Information("Resumed live task {TaskId} from persisted state.", restoredId);
                    }
                }
            }

            var extensionManager = sp.GetRequiredService<IExtensionManager>();
            await extensionManager.DiscoverExtensionsAsync(cancellationToken).ConfigureAwait(false);

            // Send manifest
            var manifest = await extensionManager.GetManifestAsync(cancellationToken).ConfigureAwait(false);
            var cloudConnector = sp.GetRequiredService<ICloudConnector>();
            await cloudConnector.SendExtensionManifestAsync(manifest, cancellationToken).ConfigureAwait(false);

            var securityManager = sp.GetRequiredService<ISecurityManager>();
            if (securityManager.IsDebuggerAttached())
            {
                Console.WriteLine("⚠️ Debugger detected. Some security features may be limited.");
            }

            if (!securityManager.VerifyIntegrity())
            {
                throw new EngineException("Binary integrity check failed.");
            }

            // Credentials must be already set via --auth in service mode
            if (!Credentials.IsAvailable)
            {
                throw new InvalidOperationException("Credentials not provided. Use --auth=username,password,apikey when running as a service.");
            }

            var cloudConnectorServices = sp.GetRequiredService<ICloudConnector>();
            await cloudConnectorServices.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Engine service cancelled.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception in EngineHostedService.");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
    }
}
