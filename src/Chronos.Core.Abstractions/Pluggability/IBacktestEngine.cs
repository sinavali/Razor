namespace Chronos.Core.Abstractions.Pluggability;

/// <summary>Backtest engine abstraction.</summary>
public interface IBacktestEngine
{
    /// <summary>Engine name.</summary>
    string EngineName { get; }

    /// <summary>Executes the backtest asynchronously.</summary>
    Task<IBacktestResult> RunAsync(IBacktestInput input, CancellationToken cancellationToken);
}
