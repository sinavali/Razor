#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Razor.Core.Engine.Core;

internal static class RuntimeEnvironment
{
    private static bool? _isDevelopment;

    public static bool IsDevelopment
    {
        get
        {
            if (_isDevelopment.HasValue)
            {
                return _isDevelopment.Value;
            }

            // Check environment variable: DOTNET_ENVIRONMENT or ASPNETCORE_ENVIRONMENT
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? "Production";

            _isDevelopment = env.Equals("Development", StringComparison.OrdinalIgnoreCase);
            return _isDevelopment.Value;
        }
    }

    public static bool IsProduction => !IsDevelopment;

    // Allow programmatic override (e.g., from command line)
    public static void SetDevelopment(bool isDevelopment)
    {
        _isDevelopment = isDevelopment;
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
