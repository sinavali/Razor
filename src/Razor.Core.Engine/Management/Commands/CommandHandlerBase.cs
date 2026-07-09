namespace Razor.Core.Engine.Management.Commands;

using Razor.Core.Engine.Communication;
using Microsoft.Extensions.Logging;

/// <summary>Base class for command handlers.</summary>
internal abstract class CommandHandlerBase : ICommandHandler
{
    protected readonly ICloudConnector CloudConnector;
    protected readonly ICommandDispatcher Dispatcher;
    protected readonly ILogger Logger;

    /// <summary>Initialises a new instance of the <see cref="CommandHandlerBase"/> class.</summary>
    protected CommandHandlerBase(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ILogger logger)
    {
        CloudConnector = cloudConnector;
        Dispatcher = dispatcher;
        Logger = logger;
    }

    public abstract int CommandId { get; }
    public abstract Task HandleAsync(CloudCommand command, CancellationToken cancellationToken);

    /// <summary>Sends a response for the current command.</summary>
    /// <param name="correlationId">Correlation identifier.</param>
    /// <param name="result">Result payload.</param>
    /// <param name="error">Error message, if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task SendResponseAsync(string correlationId, object? result, string? error = null, CancellationToken cancellationToken = default)
    {
        await Dispatcher.SendResponseAsync(CommandId, correlationId, result, error, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a success response.</summary>
    /// <param name="correlationId">Correlation identifier.</param>
    /// <param name="result">Result payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task SendSuccessAsync(string correlationId, object? result, CancellationToken cancellationToken = default)
    {
        await SendResponseAsync(correlationId, result, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends an error response.</summary>
    /// <param name="correlationId">Correlation identifier.</param>
    /// <param name="error">Error message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected async Task SendErrorAsync(string correlationId, string error, CancellationToken cancellationToken = default)
    {
        await SendResponseAsync(correlationId, null, error, cancellationToken).ConfigureAwait(false);
    }
}
