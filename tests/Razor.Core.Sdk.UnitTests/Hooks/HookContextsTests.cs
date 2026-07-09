using Razor.Core.Sdk.Hooks;

namespace Razor.Core.Sdk.UnitTests.Hooks;

public class HookContextsTests
{
    [Fact]
    public void IHookContext_Exists() => Assert.True(typeof(IHookContext).IsInterface);

    [Fact]
    public void IBacktestContext_Inherits_From_IHookContext() =>
        Assert.True(typeof(IHookContext).IsAssignableFrom(typeof(IBacktestContext)));

    [Fact]
    public void ILiveContext_Inherits_From_IHookContext() =>
        Assert.True(typeof(IHookContext).IsAssignableFrom(typeof(ILiveContext)));

    [Fact]
    public void IOptimizationContext_Inherits_From_IHookContext() =>
        Assert.True(typeof(IHookContext).IsAssignableFrom(typeof(IOptimizationContext)));

    [Fact]
    public void IReportContext_Inherits_From_IHookContext() =>
        Assert.True(typeof(IHookContext).IsAssignableFrom(typeof(IReportContext)));
}
