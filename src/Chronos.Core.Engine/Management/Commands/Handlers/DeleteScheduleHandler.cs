// -----------------------------------------------------------------------------
// <copyright file="DeleteScheduleHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Scheduling;
using Microsoft.Extensions.Logging;

internal sealed class DeleteScheduleHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public DeleteScheduleHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<DeleteScheduleHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => CommandIds.DeleteSchedule;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("ScheduleId", out object? idObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing ScheduleId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string scheduleId = idObj?.ToString()!;
        await _cronJobManager.DeleteScheduleAsync(scheduleId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { ScheduleId = scheduleId }, cancellationToken).ConfigureAwait(false);
    }
}
