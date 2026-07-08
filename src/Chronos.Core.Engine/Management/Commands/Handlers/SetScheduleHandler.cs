// -----------------------------------------------------------------------------
// <copyright file="SetScheduleHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Scheduling;
using Microsoft.Extensions.Logging;
using System.Globalization;

internal sealed class SetScheduleHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public SetScheduleHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<SetScheduleHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => CommandIds.SetSchedule;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("ScheduleId", out object? idObj) ||
            !dict.TryGetValue("ScheduledTimeUtc", out object? timeObj) ||
            !dict.TryGetValue("Command", out object? cmdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing ScheduleId, ScheduledTimeUtc, or Command.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string scheduleId = idObj?.ToString()!;
        DateTime scheduledTime = DateTime.Parse(timeObj?.ToString()!, CultureInfo.InvariantCulture);
        string cmd = cmdObj?.ToString()!;
        bool repeat = dict.TryGetValue("Repeat", out object? repeatObj) && repeatObj is bool r && r;

        await _cronJobManager.SetScheduleAsync(scheduleId, scheduledTime, cmd, repeat, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { ScheduleId = scheduleId }, cancellationToken).ConfigureAwait(false);
    }
}
