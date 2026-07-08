using Chronos.Core.Sdk.Hooks;
using Chronos.Core.Sdk.Shared;

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

    /// <summary>
    /// Clears all registered callbacks from all hook points.
    /// </summary>
    public void ClearAll()
    {
        ((FilterRegistration<ReportRequest>)OnBeforeGenerate).Clear();
        ((ActionRegistration<(byte[], string)>)OnAfterGenerate).Clear();
    }
}
