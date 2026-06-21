// -----------------------------------------------------------------------------
// <copyright file="Program.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Core.Exceptions;
using Chronos.Core.Engine.Extensions;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Scheduling;
using Chronos.Core.Engine.Management.Tasks;
using Chronos.Core.Engine.Services.BehaviorRecorder;
using Chronos.Core.Engine.Services.Mining;
using Chronos.Core.Engine.Services.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

// Resolve ambiguity between Microsoft.Extensions.Logging.ILogger and Serilog.ILogger.
using ILogger = Microsoft.Extensions.Logging.ILogger;

/// <summary>
/// Main entry point for the Chronos Engine.
/// </summary>
[SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Console output for CLI; no localization required.")]
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

    // Win32 API for maximising console window (Windows only).
    [SuppressMessage("Security", "CA5392:Use DefaultDllImportSearchPaths attribute for P/Invokes", Justification = "System DLLs are loaded from known safe paths.")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    [SuppressMessage("Security", "CA5392:Use DefaultDllImportSearchPaths attribute for P/Invokes", Justification = "System DLLs are loaded from known safe paths.")]
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_MAXIMIZE = 3;

    /// <summary>
    /// The main method.
    /// </summary>
    /// <param name="args">Command‑line arguments. Supports --auth=username:password:apikey.</param>
    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Main is async.")]
    public static async Task Main(string[] args)
    {
        // Parse --help and --version first.
        if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.Ordinal)))
        {
            PrintHelp();
            return;
        }

        if (args.Any(a => a.Equals("--version", StringComparison.OrdinalIgnoreCase) || a.Equals("-v", StringComparison.Ordinal)))
        {
            PrintVersion();
            return;
        }

        if (args.Any(a => a.Equals("--development", StringComparison.OrdinalIgnoreCase)))
        {
            Chronos.Core.Engine.Core.RuntimeEnvironment.SetDevelopment(true);
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

        _shutdownCts = new CancellationTokenSource();
        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Maximise console window (Windows only; Linux uses default size).
        MaximizeConsole();

        int exitCode = 0;
        try
        {
            _serviceProvider = BuildServiceProvider();

            // After building, initialise command dispatcher to hook up event.
            var dispatcher = _serviceProvider.GetRequiredService<ICommandDispatcher>() as CommandDispatcher;
            dispatcher?.Initialize();

            await RunEngineAsync(_serviceProvider, _shutdownCts.Token).ConfigureAwait(false);
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
        services.AddSingleton<ILoggingService, LoggingService>();

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

        // Services
        services.AddSingleton<IBehaviorRecorder, BehaviorRecorder>();
        services.AddSingleton<IMiningIntegration, MiningIntegration>();
        services.AddSingleton<ISelfUpdateManager, SelfUpdateManager>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddSerilog(dispose: true);
        });

        return services.BuildServiceProvider();
    }

    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Execution is async.")]
    private static async Task RunEngineAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        Console.WriteLine("=== Chronos Engine v1.0.0 LTS ===");
        Console.WriteLine($"Runtime: {Environment.Version}");

        var loggingService = serviceProvider.GetRequiredService<ILoggingService>();
        await loggingService.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var telemetry = serviceProvider.GetRequiredService<IEngineTelemetry>();
        telemetry.RecordStartup();

        var stateManager = serviceProvider.GetRequiredService<IStateManager>();
        await stateManager.LoadStateAsync(cancellationToken).ConfigureAwait(false);

        var extensionManager = serviceProvider.GetRequiredService<IExtensionManager>();
        await extensionManager.DiscoverExtensionsAsync(cancellationToken).ConfigureAwait(false);

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

        var cloudConnector = serviceProvider.GetRequiredService<ICloudConnector>();
        await cloudConnector.RunAsync(cancellationToken).ConfigureAwait(false);
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

    [SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Console output for CLI help; no localization required.")]
    private static void PrintHelp()
    {
        Console.WriteLine("Chronos Engine v" + AppConstants.EngineVersion);
        Console.WriteLine("Usage: Chronos.Engine [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --auth=username:password:apikey   Set credentials via command line");
        Console.WriteLine("  --help, -h                       Show this help message");
        Console.WriteLine("  --version, -v                    Show version information");
        Console.WriteLine("  --service                        Run as a Windows Service (Windows only)");
        Console.WriteLine("  --service-name=<name>            Service name (default: ChronosEngine)");
        Console.WriteLine("  --service-display=<display>      Display name (default: Chronos Engine)");
        Console.WriteLine("  --service-description=<desc>     Description (default: Chronos Trading Engine)");
    }

    [SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Console output for version; no localization required.")]
    private static void PrintVersion()
    {
        Console.WriteLine($"Chronos Engine v{AppConstants.EngineVersion} LTS");
        Console.WriteLine($"Target SDK: v{AppConstants.SdkVersion}");
        Console.WriteLine($"Runtime: {Environment.Version}");
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Platform-specific calls are guarded by RuntimeInformation checks.")]
    private static void MaximizeConsole()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_MAXIMIZE);
                }
            }
            // On Linux, we do nothing to avoid any cursor or mouse issues.
            // The terminal will use its default size, which is fine.
        }
        catch
        {
            // Ignore if console manipulation fails.
        }
    }
}
