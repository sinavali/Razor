// -----------------------------------------------------------------------------
// <copyright file="DeleteCronJobHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Razor.Core.Engine.Management.Scheduling;
using Microsoft.Extensions.Logging;

internal sealed class DeleteCronJobHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public DeleteCronJobHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<DeleteCronJobHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => CommandIds.DeleteCronJob;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        if (command.Parameters is not Dictionary<string, object> dict || !dict.TryGetValue("JobId", out object? jobIdObj))
        {
            await SendErrorAsync(command.CorrelationId ?? string.Empty, "Missing JobId.", cancellationToken).ConfigureAwait(false);
            return;
        }

        string jobId = jobIdObj?.ToString()!;
        await _cronJobManager.DeleteCronJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, new { JobId = jobId }, cancellationToken).ConfigureAwait(false);
    }
}
