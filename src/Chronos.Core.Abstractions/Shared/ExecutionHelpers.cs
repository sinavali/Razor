namespace Chronos.Core.Abstractions.Shared;

/// <summary>Threading helpers.</summary>
public static class ExecutionHelpers
{
    /// <summary>Resolves the actual parallelism level.</summary>
    /// <param name="configuredThreads">Desired thread count (0 = auto).</param>
    /// <returns>The effective thread count, leaving one core free when auto is selected.</returns>
    public static int ResolveParallelism(int configuredThreads)
        => configuredThreads > 0 ? configuredThreads : Math.Max(1, Environment.ProcessorCount - 1);
}
