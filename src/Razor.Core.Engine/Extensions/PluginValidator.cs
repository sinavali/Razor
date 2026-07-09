using Razor.Core.Sdk.Shared;
using System.Reflection;

namespace Razor.Core.Engine.Extensions;

/// <summary>
/// Validates plugin SDK version.
/// </summary>
internal static class PluginValidator
{
    public static IReadOnlyList<string> ValidateAssembly(Assembly assembly, int expectedMajor = 1)
    {
        var errors = new List<string>();
        var attr = assembly.GetCustomAttribute<SdkVersionAttribute>();
        if (attr == null)
        {
            errors.Add("Missing [SdkVersion] attribute.");
            return errors;
        }

        if (!Version.TryParse(attr.Version, out var version))
        {
            errors.Add($"Invalid SDK version format: '{attr.Version}'.");
            return errors;
        }

        if (version.Major != expectedMajor)
        {
            errors.Add($"SDK version mismatch: expected {expectedMajor}.x, got {version.Major}.x.");
        }

        return errors;
    }
}
