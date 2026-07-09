// -----------------------------------------------------------------------------
// <copyright file="ExceptionLogger.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

#pragma warning disable CA1305 // Specify IFormatProvider – logging strings are culture‑invariant and for diagnostics only.

namespace Razor.Core.Engine.Core;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Centralized exception logger with correlation ID support.
/// </summary>
internal static class ExceptionLogger
{
    private static readonly AsyncLocal<string?> _currentCorrelationId = new();

    /// <summary>
    /// Sets the current correlation ID for the executing context.
    /// </summary>
    public static void SetCorrelationId(string correlationId) => _currentCorrelationId.Value = correlationId;

    /// <summary>
    /// Gets the current correlation ID.
    /// </summary>
    public static string? GetCorrelationId() => _currentCorrelationId.Value;

    /// <summary>
    /// Logs an exception with full context.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="operation">Optional operation description.</param>
    public static void Log(Exception ex, string? operation = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Exception logged at {DateTime.UtcNow:O}");
        if (operation != null)
        {
            sb.AppendLine($"Operation: {operation}");
        }
        if (_currentCorrelationId.Value != null)
        {
            sb.AppendLine($"CorrelationId: {_currentCorrelationId.Value}");
        }
        sb.AppendLine($"Type: {ex.GetType().FullName}");
        sb.AppendLine($"Message: {ex.Message}");
        sb.AppendLine($"Stack: {ex.StackTrace}");

        if (ex.InnerException != null)
        {
            sb.AppendLine("--- Inner Exception ---");
            sb.AppendLine($"Type: {ex.InnerException.GetType().FullName}");
            sb.AppendLine($"Message: {ex.InnerException.Message}");
            sb.AppendLine($"Stack: {ex.InnerException.StackTrace}");
        }

        // Log to trace (which goes to diagnostic output and Serilog).
        Trace.TraceError(sb.ToString());

        // Also write to console in development.
        if (RuntimeEnvironment.IsDevelopment)
        {
            Console.Error.WriteLine(sb.ToString());
        }
    }

    /// <summary>
    /// Logs an informational message with correlation ID.
    /// </summary>
    public static void LogInfo(string message)
    {
        var prefix = _currentCorrelationId.Value != null ? $"[{_currentCorrelationId.Value}] " : string.Empty;
        Trace.TraceInformation($"{prefix}{message}");
        if (RuntimeEnvironment.IsDevelopment)
        {
            Console.WriteLine($"{prefix}{message}");
        }
    }

    /// <summary>
    /// Logs a warning message with correlation ID.
    /// </summary>
    public static void LogWarning(string message)
    {
        var prefix = _currentCorrelationId.Value != null ? $"[{_currentCorrelationId.Value}] " : string.Empty;
        Trace.TraceWarning($"{prefix}{message}");
        if (RuntimeEnvironment.IsDevelopment)
        {
            Console.WriteLine($"{prefix}{message}");
        }
    }
}

#pragma warning restore CA1305
