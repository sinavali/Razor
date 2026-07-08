// -----------------------------------------------------------------------------
// <copyright file="ListCronJobsHandler.cs" company="Chronos Platform">
//   Copyright (c) Chronos Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Chronos.Core.Engine.Management.Commands.Handlers;

using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Management.Commands;
using Chronos.Core.Engine.Management.Scheduling;
using Microsoft.Extensions.Logging;

internal sealed class ListCronJobsHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public ListCronJobsHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<ListCronJobsHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => CommandIds.ListCronJobs;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object jobs = await _cronJobManager.ListCronJobsAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, jobs, cancellationToken).ConfigureAwait(false);
    }
}
