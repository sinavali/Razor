using Razor.Core.Engine.Communication;

namespace Razor.Core.Engine.Management.Commands;

/// <summary>
/// Base interface for all command handlers.
/// </summary>
internal interface ICommandHandler
{
    /// <summary>Command ID this handler supports.</summary>
    int CommandId { get; }

    /// <summary>Handles the command.</summary>
    /// <returns>A task representing the asynchronous handling operation.</returns>
    Task HandleAsync(CloudCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches commands to registered handlers.
/// </summary>
internal interface ICommandDispatcher
{
    /// <summary>Dispatches a command.</summary>
    /// <returns>A task representing the asynchronous dispatch operation.</returns>
    Task DispatchAsync(CloudCommand command, CancellationToken cancellationToken);

    /// <summary>Registers a handler.</summary>
    void RegisterHandler(int commandId, ICommandHandler handler);

    /// <summary>Sends a command response.</summary>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendResponseAsync(int commandId, string correlationId, object? result, string? error = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stops all user tasks (live, backtest, optimization).</summary>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopUserTasksAsync(CancellationToken cancellationToken);
}
