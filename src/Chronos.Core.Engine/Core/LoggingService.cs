// -----------------------------------------------------------------------------
// <copyright file="LoggingService.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Core;

using System.Globalization;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

/// <summary>Service that initialises and manages Serilog logging.</summary>
internal interface ILoggingService
{
    /// <summary>Initialises the logging infrastructure.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Flushes and closes the logger.</summary>
    void Close();
}

/// <summary>Default implementation of <see cref="ILoggingService"/>.</summary>
internal sealed class LoggingService : ILoggingService, IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private bool _isInitialized;

    /// <summary>Initialises a new instance of the <see cref="LoggingService"/> class.</summary>
    /// <param name="loggerFactory">The logger factory.</param>
    public LoggingService(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return Task.CompletedTask;
        }

        string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        string logFileTemplate = Path.Combine(logDirectory, "chronos-{yyyy-MM-dd}-{deletionTimestamp}.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            // Suppress verbose internal logs.
            .MinimumLevel.Override("Chronos.Core.Engine.Extensions", LogEventLevel.Warning)
            .MinimumLevel.Override("Chronos.Core.Engine.Communication.CloudConnector", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            // Console sink removed – all console output goes through IConsoleUi.
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: logFileTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                fileSizeLimitBytes: 10485760,
                rollOnFileSizeLimit: true)
            .WriteTo.File(
                path: Path.Combine(logDirectory, "chronos-errors-.log"),
                restrictedToMinimumLevel: LogEventLevel.Error,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        _loggerFactory.AddSerilog(Log.Logger);
        _isInitialized = true;
        Log.Information("Logging service initialized.");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Close()
    {
        if (!_isInitialized)
        {
            return;
        }

        Log.CloseAndFlush();
        _isInitialized = false;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Task.Run(this.Close).ConfigureAwait(false);
    }
}
