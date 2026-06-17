using Chronos.Core.Abstractions.Shared;

namespace Chronos.Core.Abstractions.Hooks;

/// <summary>Hook registration points for the report generation pipeline.</summary>
public interface IReportHooks
{
    /// <summary>Filter the report request before generation.</summary>
    IFilterRegistration<ReportRequest> OnBeforeGenerate { get; }

    /// <summary>Action after a report is generated.</summary>
    IActionRegistration<(byte[] Data, string Format)> OnAfterGenerate { get; }
}
