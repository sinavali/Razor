// -----------------------------------------------------------------------------
// <copyright file="SetCronJobHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Razor.Core.Engine.Management.Scheduling;
using Microsoft.Extensions.Logging;

// ─── Schedules & Cron ────────────────────────────────────────────

internal sealed class SetCronJobHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public SetCronJobHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<SetCronJobHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => CommandIds.SetCronJob;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict ||
            !dict.TryGetValue("JobId", out object? jobIdObj) ||
            !dict.TryGetValue("CronExpression", out object? cronObj) ||
            !dict.TryGetValue("Command", out object? cmdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing JobId, CronExpression, or Command.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string jobId = jobIdObj?.ToString()!;
        string cron = cronObj?.ToString()!;
        string cmd = cmdObj?.ToString()!;
        bool enabled = dict.TryGetValue("Enabled", out object? enabledObj) && enabledObj is bool e && e;

        await _cronJobManager.SetCronJobAsync(jobId, cron, cmd, enabled, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { JobId = jobId }, cancellationToken).ConfigureAwait(false);
    }
}
