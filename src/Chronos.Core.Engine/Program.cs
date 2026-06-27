// -----------------------------------------------------------------------------
// <copyright file="Program.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine;

using Abstractions.Hooks;
using Chronos.Core.Engine.Kernel;
using Chronos.Core.Kernel.Hooks;
using Chronos.Core.Kernel.Messaging;
using Chronos.Core.Kernel.Telemetry;
using Communication;
using Core;
using Core.Exceptions;
using Extensions;
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
using ILogger = Microsoft.Extensions.Logging.ILogger;

/// <summary>
/// Main entry point for the Chronos Engine.
/// </summary>
[SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
    Justification = "Console output for CLI; no localization required.")]
internal sealed class Program
{
    private static IServiceProvider? _serviceProvider;
    private static CancellationTokenSource? _shutdownCts;

    // LoggerMessage delegates for shutdown.
    private static readonly Action<ILogger, Exception?> _logShutdownTasksError =
        LoggerMessage.Define(LogLevel.Error, 0, "Error stopping tasks during shutdown.");

    private static readonly Action<ILogger, Exception?> _logShutdownCloudError =
        LoggerMessage.Define(LogLevel.Error, 1, "Error disconnecting from cloud.");

    private static readonly Action<ILogger, Exception?> _logShutdownStateError =
        LoggerMessage.Define(LogLevel.Error, 2, "Error saving state during shutdown.");

    private static readonly Action<ILogger, Exception?> _logShutdownBehaviorError =
        LoggerMessage.Define(LogLevel.Error, 3, "Error flushing behavior records during shutdown.");

    /// <summary>
    /// The main method.
    /// </summary>
    /// <param name="args">Command‑line arguments. Supports --auth=username:password:apikey.</param>
    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
        Justification = "Main is async.")]
    public static async Task Main(string[] args)
    {
        // Parse --help and --version first.
        if (args.Any(a =>
                a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.Ordinal)))
        {
            PrintHelp();
            return;
        }

        if (args.Any(a =>
                a.Equals("--version", StringComparison.OrdinalIgnoreCase) || a.Equals("-v", StringComparison.Ordinal)))
        {
            PrintVersion();
            return;
        }

        if (args.Any(a => a.Equals("--development", StringComparison.OrdinalIgnoreCase)))
        {
            RuntimeEnvironment.SetDevelopment(true);
        }

#if DEBUG
        AppConstants.IsDevelopment = true;
#endif

        // Parse --auth=username,password,apikey
        string? authArg = args.FirstOrDefault(a => a.StartsWith("--auth=", StringComparison.OrdinalIgnoreCase));
        if (authArg != null)
        {
            string[] parts = authArg.Substring("--auth=".Length).Split(',');
            if (parts.Length == 3)
            {
                Credentials.SetCredentials(parts[0], parts[1], parts[2]);
            }
            else
            {
                Console.WriteLine("Invalid --auth format. Expected username,password,apikey");
                Environment.ExitCode = 1;
                return;
            }
        }

        // Parse --command=restart (for self‑update).
        bool isRestart = args.Any(a => a.Equals("--command=restart", StringComparison.OrdinalIgnoreCase));
        if (isRestart)
        {
            Log.Information("Engine restarting after update.");
        }

        // Detect --service flag
        bool isService = args.Any(a => a.Equals("--service", StringComparison.OrdinalIgnoreCase));

        _shutdownCts = new CancellationTokenSource();
        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        int exitCode = 0;
        try
        {
            _serviceProvider = BuildServiceProvider();

            // If this is a restart after update, finalize the update
            if (isRestart)
            {
                var selfUpdate = _serviceProvider.GetRequiredService<ISelfUpdateManager>();
                await selfUpdate.FinalizeUpdateAsync(_shutdownCts.Token).ConfigureAwait(false);
            }

            if (isService)
            {
                // Run as a hosted service (Windows Service / systemd)
                await RunAsServiceAsync(_serviceProvider, _shutdownCts.Token).ConfigureAwait(false);
            }
            else
            {
                // Run as a console application
                await RunConsoleAsync(_serviceProvider, _shutdownCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Engine shutdown requested.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception in Main.");
            exitCode = 1;
        }
        finally
        {
            await ShutdownEngineAsync().ConfigureAwait(false);
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }

        Environment.ExitCode = exitCode;
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Core
        services.AddSingleton<ISecurityManager, SecurityManager>();
        services.AddSingleton<IStateManager, StateManager>();
        services.AddSingleton<IEngineTelemetry, EngineTelemetry>();
        services.AddSingleton<BinaryTransferManager>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<ConfigStore>();

        // Communication
        services.AddSingleton<ICloudConnector, CloudConnector>();

        // Management
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<Lazy<ICommandDispatcher>>(sp =>
            new Lazy<ICommandDispatcher>(() => sp.GetRequiredService<ICommandDispatcher>()));
        services.AddSingleton<ICronJobManager, CronJobManager>();
        services.AddSingleton<ITaskManager, TaskManager>();

        // Extensions
        services.AddSingleton<IExtensionManager, ExtensionManager>();

        // Kernel
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<ICoreMetrics>(sp => new CoreMetrics("engine"));
        services.AddSingleton<IKernelService, KernelService>();

        // Services
        services.AddSingleton<IBehaviorRecorder>(sp =>
            new BehaviorRecorder(
                sp.GetRequiredService<ILogger<BehaviorRecorder>>(),
                sp.GetRequiredService<ICloudConnector>()));
        services.AddSingleton<IMiningIntegration, MiningIntegration>();
        services.AddSingleton<ISelfUpdateManager, SelfUpdateManager>();

        // Hooks
        services.AddSingleton<IHookRegistry, HookRegistry>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddSerilog(dispose: true);
        });

        // Hosted service (for service mode)
        services.AddHostedService<EngineHostedService>();

        return services.BuildServiceProvider();
    }

    private static async Task RunConsoleAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        Console.WriteLine("=== Chronos Engine v1.0.0 LTS ===");
        Console.WriteLine($"Runtime: {Environment.Version}");

        var loggingService = serviceProvider.GetRequiredService<ILoggingService>();
        await loggingService.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var telemetry = serviceProvider.GetRequiredService<IEngineTelemetry>();
        telemetry.RecordStartup();

        var stateManager = serviceProvider.GetRequiredService<IStateManager>();
        await stateManager.LoadStateAsync(cancellationToken).ConfigureAwait(false);

        // Restore live state
        var liveState = await stateManager.LoadLiveStateAsync(cancellationToken).ConfigureAwait(false);
        if (liveState != null)
        {
            var taskManager = serviceProvider.GetRequiredService<ITaskManager>() as TaskManager;
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

        var extensionManager = serviceProvider.GetRequiredService<IExtensionManager>();
        await extensionManager.DiscoverExtensionsAsync(cancellationToken).ConfigureAwait(false);

        // Send manifest to Cloud
        var manifest = await extensionManager.GetManifestAsync(cancellationToken).ConfigureAwait(false);
        var cloudConnector = serviceProvider.GetRequiredService<ICloudConnector>();
        await cloudConnector.SendExtensionManifestAsync(manifest, cancellationToken).ConfigureAwait(false);

        var securityManager = serviceProvider.GetRequiredService<ISecurityManager>();

        if (securityManager.IsDebuggerAttached())
        {
            Console.WriteLine("⚠️ Debugger detected. Some security features may be limited.");
        }

        if (!securityManager.VerifyIntegrity())
        {
            throw new EngineException("Binary integrity check failed.");
        }

        // If credentials were not provided via command line, prompt the user.
        if (!Credentials.IsAvailable)
        {
            Credentials.PromptForCredentials();
        }

        // Reuse the same cloud connector instance
        await cloudConnector.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunAsServiceAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Rebuild the host with our services.
        using var host2 = Host.CreateDefaultBuilder()
            .UseWindowsService(options =>
            {
                options.ServiceName = "Chronos Engine";
            })
            .UseSystemd()
            .ConfigureServices((context, services) =>
            {
                // Register all our services.
                services.AddSingleton<ISecurityManager, SecurityManager>();
                services.AddSingleton<IStateManager, StateManager>();
                services.AddSingleton<IEngineTelemetry, EngineTelemetry>();
                services.AddSingleton<BinaryTransferManager>();
                services.AddSingleton<ILoggingService, LoggingService>();
                services.AddSingleton<ICloudConnector, CloudConnector>();
                services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
                services.AddSingleton<Lazy<ICommandDispatcher>>(sp =>
                    new Lazy<ICommandDispatcher>(() => sp.GetRequiredService<ICommandDispatcher>()));
                services.AddSingleton<ICronJobManager, CronJobManager>();
                services.AddSingleton<ITaskManager, TaskManager>();
                services.AddSingleton<IExtensionManager, ExtensionManager>();
                services.AddSingleton<IMessageBus, MessageBus>();
                services.AddSingleton<ICoreMetrics>(sp => new CoreMetrics("engine"));
                services.AddSingleton<IKernelService, KernelService>();
                services.AddSingleton<IBehaviorRecorder>(sp =>
                    new BehaviorRecorder(
                        sp.GetRequiredService<ILogger<BehaviorRecorder>>(),
                        sp.GetRequiredService<ICloudConnector>()));
                services.AddSingleton<IMiningIntegration, MiningIntegration>();
                services.AddSingleton<ISelfUpdateManager, SelfUpdateManager>();
                services.AddSingleton<IHookRegistry, HookRegistry>();
                services.AddHostedService<EngineHostedService>();
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddSerilog(dispose: true);
                });
            })
            .Build();

        await host2.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ShutdownEngineAsync()
    {
        Log.Information("Shutting down engine...");

        var logger = _serviceProvider?.GetService<ILogger<Program>>();

        try
        {
            var taskManager = _serviceProvider?.GetRequiredService<ITaskManager>();
            if (taskManager != null)
            {
                await taskManager.StopAllTasksAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (logger != null)
            {
                _logShutdownTasksError(logger, ex);
            }
            else
            {
                Log.Error(ex, "Error stopping tasks during shutdown.");
            }
        }

        try
        {
            var cloudConnector = _serviceProvider?.GetRequiredService<ICloudConnector>();
            if (cloudConnector != null)
            {
                await cloudConnector.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (logger != null)
            {
                _logShutdownCloudError(logger, ex);
            }
            else
            {
                Log.Error(ex, "Error disconnecting from cloud.");
            }
        }

        try
        {
            var stateManager = _serviceProvider?.GetRequiredService<IStateManager>();
            if (stateManager != null)
            {
                await stateManager.SaveStateAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (logger != null)
            {
                _logShutdownStateError(logger, ex);
            }
            else
            {
                Log.Error(ex, "Error saving state during shutdown.");
            }
        }

        try
        {
            var behaviorRecorder = _serviceProvider?.GetRequiredService<IBehaviorRecorder>();
            if (behaviorRecorder != null)
            {
                behaviorRecorder.Disable();
                await behaviorRecorder.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (logger != null)
            {
                _logShutdownBehaviorError(logger, ex);
            }
            else
            {
                Log.Error(ex, "Error flushing behavior records during shutdown.");
            }
        }

        Console.WriteLine("Engine shutdown complete.");
        _shutdownCts?.Dispose();
        _shutdownCts = null;
    }

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Log.Information("Cancel key pressed. Shutting down...");
        _shutdownCts?.Cancel();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Fatal(ex!, "Unhandled exception. The engine is shutting down.");
        _shutdownCts?.Cancel();
    }

    [SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
        Justification = "Console output for CLI help; no localization required.")]
    private static void PrintHelp()
    {
        Console.WriteLine("Chronos Engine v" + AppConstants.EngineVersion);
        Console.WriteLine("Usage: Chronos.Engine [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --auth=username,password,apikey   Set credentials via command line (required for service mode)");
        Console.WriteLine("  --help, -h                       Show this help message");
        Console.WriteLine("  --version, -v                    Show version information");
        Console.WriteLine("  --service                        Run as a Windows Service (Windows) or systemd (Linux)");
        Console.WriteLine("  --development                    Run in development mode (disable some security checks)");
        Console.WriteLine("  --command=restart                Internal use for self-update");
        Console.WriteLine("\nService installation (Windows):");
        Console.WriteLine("  sc create ChronosEngine binPath= \"C:\\Path\\Chronos.Engine.exe --service --auth=user,pass,key\"");
        Console.WriteLine("\nService installation (Linux):");
        Console.WriteLine("  Create /etc/systemd/system/chronos.service with:");
        Console.WriteLine("  [Service]");
        Console.WriteLine("  ExecStart=/opt/chronos/Chronos.Engine --service --auth=user,pass,key");
        Console.WriteLine("  WorkingDirectory=/opt/chronos");
    }

    [SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
        Justification = "Console output for version; no localization required.")]
    private static void PrintVersion()
    {
        Console.WriteLine($"Chronos Engine v{AppConstants.EngineVersion} LTS");
        Console.WriteLine($"Target SDK: v{AppConstants.SdkVersion}");
        Console.WriteLine($"Runtime: {Environment.Version}");
    }
}
