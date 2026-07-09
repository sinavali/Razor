// -----------------------------------------------------------------------------
// <copyright file="ListSchedulesHandler.cs" company="Razor Platform">
//   Copyright (c) Razor Platform. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------------

namespace Razor.Core.Engine.Management.Commands.Handlers;

using Razor.Core.Engine.Communication;
using Razor.Core.Engine.Management.Commands;
using Razor.Core.Engine.Management.Scheduling;
using Microsoft.Extensions.Logging;

internal sealed class ListSchedulesHandler : CommandHandlerBase
{
    private readonly ICronJobManager _cronJobManager;

    public ListSchedulesHandler(ICloudConnector cloudConnector, ICommandDispatcher dispatcher, ICronJobManager cronJobManager, ILogger<ListSchedulesHandler> logger)
        : base(cloudConnector, dispatcher, logger)
    {
        _cronJobManager = cronJobManager;
    }

    public override int CommandId => CommandIds.ListSchedules;

    public override async Task HandleAsync(CloudCommand command, CancellationToken cancellationToken)
    {
        object schedules = await _cronJobManager.ListSchedulesAsync(cancellationToken).ConfigureAwait(false);
        await SendSuccessAsync(command.CorrelationId ?? string.Empty, schedules, cancellationToken).ConfigureAwait(false);
    }
}
