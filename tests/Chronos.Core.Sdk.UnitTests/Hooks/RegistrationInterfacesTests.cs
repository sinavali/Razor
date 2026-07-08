using Chronos.Core.Sdk.Hooks;

namespace Chronos.Core.Sdk.UnitTests.Hooks;

public class RegistrationInterfacesTests
{
    [Fact]
    public void IActionRegistration_T_Is_Interface() =>
        Assert.True(typeof(IActionRegistration<int>).IsInterface);

    [Fact]
    public void IActionRegistration_Is_Interface() =>
        Assert.True(typeof(IActionRegistration).IsInterface);

    [Fact]
    public void IFilterRegistration_T_Is_Interface() =>
        Assert.True(typeof(IFilterRegistration<string>).IsInterface);

    [Fact]
    public void IHookRegistry_Exposes_All_Sub_Registries()
    {
        Assert.NotNull(typeof(IHookRegistry).GetProperty("Backtest"));
        Assert.NotNull(typeof(IHookRegistry).GetProperty("Live"));
        Assert.NotNull(typeof(IHookRegistry).GetProperty("Optimization"));
        Assert.NotNull(typeof(IHookRegistry).GetProperty("Report"));
    }
}
