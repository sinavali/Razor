// -----------------------------------------------------------------------------
// <copyright file="AuditTrailLoggingHookPlugin.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;

namespace Razor.Core.Engine.Hooks;

/// <summary>
/// Hook plugin that writes an immutable, append‑only audit trail of every live broker order
/// event (execution or rejection) to a SQLite database for regulatory compliance.
/// Registered automatically by the engine via <see cref="IHookManifest.RegisterHooks"/>.
/// </summary>
internal sealed class AuditTrailLoggingHookPlugin : IHookManifest, IDisposable
{
    private readonly SqliteAuditTrailStore _store;
    private readonly ILogger _logger;
    private readonly string _taskId;

    private static readonly Action<ILogger, long, Exception?> _logExecutedFailed =
        LoggerMessage.Define<long>(LogLevel.Error, 0, "Audit trail: failed to record executed order {OrderId}.");
    private static readonly Action<ILogger, string, Exception?> _logRejectedFailed =
        LoggerMessage.Define<string>(LogLevel.Error, 1, "Audit trail: failed to record rejected order for {Symbol}.");

    /// <summary>
    /// Creates a new audit trail logging hook plugin.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file (default: <c>./audit_trail.db</c>).</param>
    /// <param name="taskId">Task identifier used when the live session does not supply one.</param>
    /// <param name="logger">Optional logger.</param>
    public AuditTrailLoggingHookPlugin(string? databasePath = null, string taskId = "live", ILogger? logger = null)
    {
        _store = new SqliteAuditTrailStore(databasePath);
        _taskId = string.IsNullOrWhiteSpace(taskId) ? "live" : taskId;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public void RegisterHooks(IHookRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Live.OnOrderExecuted.Register(OnOrderExecuted);
        registry.Live.OnOrderRejected.Register(OnOrderRejected);
    }

    /// <summary>
    /// Returns all recorded audit trail entries in insertion order.
    /// Intended for verification and diagnostics; the underlying store is append‑only.
    /// </summary>
    /// <returns>The immutable list of records.</returns>
    public IReadOnlyList<AuditTrailRecord> ReadAllFromStore() => _store.ReadAll();

    private void OnOrderExecuted(ExecutionReport report, IHookContext context)
    {
        try
        {
            _store.Append(new AuditTrailRecord
            {
                TimestampUtc = ToUtcIso(report.Timestamp),
                TaskId = _taskId,
                OrderId = report.Ticket,
                Symbol = report.Symbol,
                Side = report.Type.ToString(),
                Quantity = report.ExecutedVolume,
                Price = report.ExecutedPrice,
                Status = "executed",
                RejectionReason = string.Empty
            });
        }
        catch (Exception ex)
        {
            _logExecutedFailed(_logger, report.Ticket, ex);
        }
    }

    private void OnOrderRejected((AdapterOrderRequest Request, string Reason) data, IHookContext context)
    {
        try
        {
            _store.Append(new AuditTrailRecord
            {
                TimestampUtc = ToUtcIso(context.UtcNow.Ticks),
                TaskId = _taskId,
                OrderId = 0,
                Symbol = data.Request.Symbol,
                Side = data.Request.Type.ToString(),
                Quantity = data.Request.Volume,
                Price = data.Request.Price,
                Status = "rejected",
                RejectionReason = data.Reason
            });
        }
        catch (Exception ex)
        {
            _logRejectedFailed(_logger, data.Request.Symbol, ex);
        }
    }

    private static string ToUtcIso(long timestampTicks)
    {
        return new DateTime(timestampTicks, DateTimeKind.Utc).ToString("O");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _store.Dispose();
    }
}
