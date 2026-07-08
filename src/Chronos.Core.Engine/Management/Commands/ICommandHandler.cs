using Chronos.Core.Engine.Communication;

namespace Chronos.Core.Engine.Management.Commands;

/// <summary>
/// Base interface for all command handlers.
/// </summary>
internal interface ICommandHandler
{
    /// <summary>Command ID this handler supports.</summary>
    int CommandId { get; }

    /// <summary>Handles the command.</summary>
    Task HandleAsync(CloudCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches commands to registered handlers.
/// </summary>
internal abstract class ICommandDispatcher
{
    /// <summary>Dispatches a command.</summary>
    public abstract Task DispatchAsync(CloudCommand command, CancellationToken cancellationToken);

    /// <summary>Registers a handler.</summary>
    public abstract void RegisterHandler(int commandId, ICommandHandler handler);

    /// <summary>Sends a command response.</summary>
    public abstract Task SendResponseAsync(int commandId, string correlationId, object? result, string? error = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stops all user tasks (live, backtest, optimization).</summary>
    public abstract Task StopUserTasksAsync(CancellationToken cancellationToken);
}
