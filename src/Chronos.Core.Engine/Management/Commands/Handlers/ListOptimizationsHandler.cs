// -----------------------------------------------------------------------------
// <copyright file="ListOptimizationsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Tasks;
using Microsoft.Extensions.Logging;

internal sealed class ListOptimizationsHandler : CommandHandlerBase
{
    private readonly ITaskManager _taskManager;

    public ListOptimizationsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ITaskManager taskManager, ILogger<ListOptimizationsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _taskManager = taskManager;
    }

    public override int CommandId => 1306;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        var tasks = _taskManager.AllTasks.Where(t => t.TaskType == "Optimization").Select(t => new
        {
            t.TaskId,
            State = t.State.ToString(),
            t.StartTime,
            t.EndTime
        }).ToArray();

        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { Tasks = tasks }, cancellationToken).ConfigureAwait(false);
    }
}
