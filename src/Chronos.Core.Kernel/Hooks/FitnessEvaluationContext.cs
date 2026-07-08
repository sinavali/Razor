using Chronos.Core.Kernel.Clock;
using Chronos.Core.Sdk.Hooks;

namespace Chronos.Core.Kernel.Hooks;

/// <summary>
/// Implementation of <see cref="IFitnessEvaluationContext"/>.
/// </summary>
public sealed class FitnessEvaluationContext : IFitnessEvaluationContext
{
    /// <inheritdoc/>
    public Chromosome Chromosome { get; }

    /// <inheritdoc/>
    public double Fitness { get; set; } = double.NaN;

    /// <inheritdoc/>
    public string HookName { get; }

    /// <inheritdoc/>
    public DateTime UtcNow { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new fitness evaluation context.
    /// </summary>
    public FitnessEvaluationContext(
        SystemClock clock,
        Chromosome chromosome,
        string hookName = "",
        CancellationToken cancellationToken = default)
    {
        Chromosome = chromosome ?? throw new ArgumentNullException(nameof(chromosome));
        HookName = hookName;
        UtcNow = clock?.GetUtcNow() ?? DateTime.UtcNow;
        CancellationToken = cancellationToken;
    }
}
