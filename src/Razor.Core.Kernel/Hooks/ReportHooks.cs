using Razor.Core.Sdk.Hooks;
using Razor.Core.Sdk.Shared;

namespace Razor.Core.Kernel.Hooks;

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
