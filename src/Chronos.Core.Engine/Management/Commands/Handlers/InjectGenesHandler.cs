// -----------------------------------------------------------------------------
// <copyright file="InjectGenesHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

internal sealed class InjectGenesHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    private static readonly Action<ILogger, int, Exception?> _logInjectingGenes =
        LoggerMessage.Define<int>(LogLevel.Information, 0, "Injecting {Count} genes into live task.");

    public InjectGenesHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<InjectGenesHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1102;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("TaskId", out object? taskIdObj) ||
            !dict.TryGetValue("Genes", out object? genesObj) ||
            genesObj is not double[] genes)
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing or invalid TaskId or Genes parameter.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string taskId = taskIdObj?.ToString()!;
        _logInjectingGenes(Logger, genes.Length, null);

        await _taskManager.InjectGenesAsync(taskId, genes, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { GenesCount = genes.Length }, cancellationToken).ConfigureAwait(false);
    }
}
