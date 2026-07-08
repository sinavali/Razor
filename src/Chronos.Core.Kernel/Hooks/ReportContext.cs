using Chronos.Core.Kernel.Clock;
using Chronos.Core.Sdk.Hooks;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Context provided to report generation hook callbacks.
/// </summary>
public sealed class ReportContext : IReportContext
{
    /// <inheritdoc/>
    public string ReportFormat { get; }

    /// <inheritdoc/>
    public string HookName { get; }

    /// <inheritdoc/>
    public DateTime UtcNow { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new report context.
    /// </summary>
    public ReportContext(
        SystemClock clock,
        string reportFormat,
        string hookName = "",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ReportFormat = reportFormat;
        HookName = hookName;
        UtcNow = clock.GetUtcNow();
        CancellationToken = cancellationToken;
    }
}
