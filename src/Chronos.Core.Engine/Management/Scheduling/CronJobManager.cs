using System.Collections.Concurrent;
using System.Text.Json;
using Chronos.Core.Engine.Communication;
using Chronos.Core.Engine.Core;
using Chronos.Core.Engine.Management.Commands;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Chronos.Core.Engine.Management.Scheduling;

internal interface ICronJobManager
{
    Task SetCronJobAsync(string jobId, string cronExpression, string command, bool enabled, CancellationToken cancellationToken);
    Task DeleteCronJobAsync(string jobId, CancellationToken cancellationToken);
    Task<object> ListCronJobsAsync(CancellationToken cancellationToken);
    Task SetScheduleAsync(string scheduleId, DateTime scheduledTimeUtc, string command, bool repeat, CancellationToken cancellationToken);
    Task DeleteScheduleAsync(string scheduleId, CancellationToken cancellationToken);
    Task<object> ListSchedulesAsync(CancellationToken cancellationToken);
}

internal sealed class CronJobManager : ICronJobManager, IDisposable
{
    private readonly ILogger<CronJobManager> _logger;
    private readonly Lazy<ICommandDispatcher> _commandDispatcher;
    private readonly IStateManager _stateManager;
    private readonly ConcurrentDictionary<string, (CrontabSchedule Schedule, string Command, bool Enabled)> _cronJobs = new();
    private readonly Timer _timer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> _logCronJobFailedToLoad =
        LoggerMessage.Define<string>(LogLevel.Error, 0, "Failed to load cron job {JobId}.");
    private static readonly Action<ILogger, string, Exception?> _logExecutingCronJob =
        LoggerMessage.Define<string>(LogLevel.Information, 1, "Executing cron job {JobId}.");
    private static readonly Action<ILogger, string, Exception?> _logErrorCheckingCronJob =
        LoggerMessage.Define<string>(LogLevel.Error, 2, "Error checking cron job {JobId}.");

    public CronJobManager(
        ILogger<CronJobManager> logger,
        Lazy<ICommandDispatcher> commandDispatcher,
        IStateManager stateManager)
    {
        _logger = logger;
        _commandDispatcher = commandDispatcher;
        _stateManager = stateManager;
        _timer = new Timer(CheckCronJobs, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        LoadCronJobsFromState().GetAwaiter().GetResult();
        LoadSchedulesFromState().GetAwaiter().GetResult();
    }

    private async Task LoadCronJobsFromState()
    {
        var jobs = await _stateManager.LoadCronJobsAsync(CancellationToken.None).ConfigureAwait(false);
        foreach (var job in jobs)
        {
            try
            {
                var schedule = CrontabSchedule.Parse(job.CronExpression, new CrontabSchedule.ParseOptions { IncludingSeconds = false });
                _cronJobs[job.JobId] = (schedule, job.Command, job.Enabled);
            }
            catch (Exception ex)
            {
                _logCronJobFailedToLoad(_logger, job.JobId, ex);
            }
        }
    }

    private async Task LoadSchedulesFromState()
    {
        var schedules = await _stateManager.LoadSchedulesAsync(CancellationToken.None).ConfigureAwait(false);
        foreach (var schedule in schedules)
        {
            var delay = schedule.ScheduledTimeUtc - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    if (!_disposed)
                    {
                        var cmd = JsonSerializer.Deserialize<CloudCommand>(schedule.Command);
                        if (cmd != null)
                        {
                            await _commandDispatcher.Value.DispatchAsync(cmd, CancellationToken.None).ConfigureAwait(false);
                        }
                        if (!schedule.Repeat)
                        {
                            await DeleteScheduleAsync(schedule.ScheduleId, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                }, CancellationToken.None);
            }
        }
    }

    private async void CheckCronJobs(object? state)
    {
        if (_disposed)
        {
            return;
        }

        if (!await _lock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            foreach (var (jobId, job) in _cronJobs.ToArray())
            {
                if (!job.Enabled)
                {
                    continue;
                }

                try
                {
                    var next = job.Schedule.GetNextOccurrence(now);
                    if (next <= now.AddSeconds(10))
                    {
                        _logExecutingCronJob(_logger, jobId, null);
                        var command = JsonSerializer.Deserialize<CloudCommand>(job.Command);
                        if (command != null)
                        {
                            await _commandDispatcher.Value.DispatchAsync(command, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logErrorCheckingCronJob(_logger, jobId, ex);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetCronJobAsync(string jobId, string cronExpression, string command, bool enabled, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var schedule = CrontabSchedule.Parse(cronExpression, new CrontabSchedule.ParseOptions { IncludingSeconds = false });
            _cronJobs[jobId] = (schedule, command, enabled);
            await _stateManager.SaveCronJobAsync(new CronJob(jobId, cronExpression, command, enabled), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteCronJobAsync(string jobId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cronJobs.TryRemove(jobId, out _);
            await _stateManager.DeleteCronJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<object> ListCronJobsAsync(CancellationToken cancellationToken)
    {
        var jobs = _cronJobs.Select(j => new { JobId = j.Key, j.Value.Command, j.Value.Enabled }).ToArray();
        return Task.FromResult<object>(new { Jobs = jobs });
    }

    public async Task SetScheduleAsync(string scheduleId, DateTime scheduledTimeUtc, string command, bool repeat, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var schedule = new Schedule(scheduleId, scheduledTimeUtc, command, repeat);
            await _stateManager.SaveScheduleAsync(schedule, cancellationToken).ConfigureAwait(false);

            var delay = scheduledTimeUtc - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    if (!cancellationToken.IsCancellationRequested && !_disposed)
                    {
                        var cmd = JsonSerializer.Deserialize<CloudCommand>(command);
                        if (cmd != null)
                        {
                            await _commandDispatcher.Value.DispatchAsync(cmd, CancellationToken.None).ConfigureAwait(false);
                        }
                        if (!repeat)
                        {
                            await DeleteScheduleAsync(scheduleId, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                }, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteScheduleAsync(string scheduleId, CancellationToken cancellationToken)
    {
        await _stateManager.DeleteScheduleAsync(scheduleId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<object> ListSchedulesAsync(CancellationToken cancellationToken)
    {
        var schedules = await _stateManager.LoadSchedulesAsync(cancellationToken).ConfigureAwait(false);
        var res = schedules.Select(s => new { s.ScheduleId, s.ScheduledTimeUtc, s.Command, s.Repeat }).ToArray();
        return new { Schedules = res };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Dispose();
        _lock.Dispose();
    }
}
