using Chronos.Core.Abstractions.Hooks;
using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Report generation hook registration points.
/// </summary>
public sealed class ReportHooks : IReportHooks
{
    /// <inheritdoc/>
    public IFilterRegistration<ReportRequest> OnBeforeGenerate { get; } = new FilterRegistration<ReportRequest>();

    /// <inheritdoc/>
    public IActionRegistration<(byte[] Data, string Format)> OnAfterGenerate { get; }
        = new ActionRegistration<(byte[], string)>();
}
