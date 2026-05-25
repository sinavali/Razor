using System.Reflection;
using Chronos.Core.Abstractions.Plugins;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// Validates plugin assemblies against the expected Chronos SDK version.
/// </summary>
public static class PluginValidator
{
    /// <summary>
    /// Checks that the assembly declares a compatible <see cref="ChronosSdkVersionAttribute"/>.
    /// </summary>
    /// <param name="assembly">The plugin assembly to validate.</param>
    /// <param name="expectedMajor">The major version required by the host (default 1).</param>
    /// <returns>A list of error messages; empty if validation passes.</returns>
    public static IReadOnlyList<string> ValidateAssembly(Assembly assembly, int expectedMajor = 1)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var errors = new List<string>();

        var attr = assembly.GetCustomAttribute<ChronosSdkVersionAttribute>();
        if (attr is null)
        {
            errors.Add("Assembly is missing [ChronosSdkVersion] attribute.");
            return errors;
        }

        if (!Version.TryParse(attr.Version, out var declaredVersion))
        {
            errors.Add($"Invalid SDK version format: '{attr.Version}'.");
            return errors;
        }

        if (declaredVersion.Major != expectedMajor)
        {
            errors.Add(
                $"SDK version mismatch: assembly targets {declaredVersion.Major}.x, but host requires {expectedMajor}.x.");
        }

        // In future, additional compatibility checks can be added here.

        return errors;
    }
}
