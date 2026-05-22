namespace Chronos.Abstractions.Shared;

/// <summary>Threading helpers.</summary>
public static class ExecutionHelpers
{
    /// <summary>Resolves the actual parallelism level.</summary>
    /// <param name="configuredThreads">Desired thread count (0 = auto).</param>
    /// <returns>The effective thread count.</returns>
    public static int ResolveParallelism(int configuredThreads)
        => configuredThreads > 0 ? configuredThreads : Environment.ProcessorCount;
}