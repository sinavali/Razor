using System.Reflection;
using Chronos.Core.Abstractions.Plugins;

namespace Chronos.Core.Kernel.Plugins;

/// <summary>
/// ARCH‑02 / GAP‑02: Validates plugin assemblies against the expected Chronos SDK version.
/// Provides central validation authority.
/// </summary>
public static class PluginValidator
{
    /// <summary>
    /// Checks that the assembly declares a compatible <see cref="ChronosSdkVersionAttribute"/>.
    /// </summary>
    /// <param name="assembly">The plugin assembly to validate.</param>
    /// <param name="expectedMajor">The major version required by the host (default 1).</param>
    /// <param name="strictVersion">
    /// If <c>true</c>, also require the minor version to be at least <paramref name="expectedMinor"/>
    /// (default 0). When <c>false</c>, any minor version within the same major is accepted.
    /// </param>
    /// <param name="expectedMinor">Minor version used when <paramref name="strictVersion"/> is <c>true</c> (default 0).</param>
    /// <returns>A list of error messages; empty if validation passes.</returns>
    public static IReadOnlyList<string> ValidateAssembly(Assembly assembly, int expectedMajor = 1,
        bool strictVersion = false, int expectedMinor = 0)
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
            return errors;
        }

        if (strictVersion && declaredVersion.Minor < expectedMinor)
        {
            errors.Add(
                $"SDK minor version too old: assembly targets {declaredVersion.Major}.{declaredVersion.Minor}, " +
                $"but host requires at least {expectedMajor}.{expectedMinor}.");
        }

        return errors;
    }
}
