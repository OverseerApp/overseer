using log4net;
using Overseer.Server.Channels;
using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Services;

public class MachineJobService(IDataContext dataContext, IMachineStatusChannel machineStatusChannel, INotificationChannel notificationChannel)
  : BackgroundService
{
  static readonly int JobUpdateIntervalMilliseconds = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;

  static readonly ILog Log = LogManager.GetLogger(typeof(MachineJobService));

  static readonly Guid _subscriberId = Guid.NewGuid();

  readonly IRepository<MachineJob> _repository = dataContext.Repository<MachineJob>();

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      MachineStatus? status = null;
      try
      {
        status = await machineStatusChannel.ReadAsync(_subscriberId, stoppingToken);
        if (status is null)
          continue;

        var job = _repository.Get(x => x.MachineId == status.MachineId && !x.EndTime.HasValue);
        async Task NotifyJobEvent(JobNotificationType type, string? message = null)
        {
          await notificationChannel.WriteAsync(
            new JobNotification
            {
              Type = type,
              MachineId = status.MachineId,
              MachineJobId = job!.Id,
              Message = message,
            },
            stoppingToken
          );
        }

        if (job is null)
        {
          // the machine is idle and there is no job in progress, nothing to do
          if (status.State == MachineState.Idle)
            continue;

          // the machine is offline and there is no job in progress, nothing to do
          if (status.State == MachineState.Offline)
            continue;

          // the machine is operational and there is no job in progress, create a new job
          job = new MachineJob
          {
            MachineId = status.MachineId,
            StartTime = DateTimeOffset.UtcNow.AddMilliseconds(-status.ElapsedJobTime).ToUnixTimeMilliseconds(),
            LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastStatus = status,
            LastNotificationType = JobNotificationType.JobStarted,
          };
          _repository.Create(job);

          await NotifyJobEvent(JobNotificationType.JobStarted);
          continue;
        }

        // the machine is operational and there is no change in state, nothing to do
        if (job.State == status.State)
        {
          // Adding every update to the database can lead to performance issues,
          // so we only update the job if the last update was more than JobUpdateIntervalMilliseconds
          var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
          if (!job.LastUpdate.HasValue || (now - job.LastUpdate.Value) >= JobUpdateIntervalMilliseconds)
          {
            job.LastUpdate = now;
            job.LastStatus = status;
            _repository.Update(job);
          }
          continue;
        }

        // otherwise, the machine is operational and the state has changed
        switch (status.State)
        {
          case MachineState.Offline:
            // not ending the job here, just marking it as offline
            // job.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            job.State = MachineState.Offline;
            await NotifyJobEvent(JobNotificationType.JobError, "job.machineOffline");
            break;

          case MachineState.Idle:
            if (job.LastUpdate.HasValue && job.LastStatus is not null)
            {
              // this should be more accurate than using the current time
              job.EndTime = DateTimeOffset
                .FromUnixTimeMilliseconds(job.LastUpdate.Value)
                .AddMilliseconds(job.LastStatus.ElapsedJobTime)
                .ToUnixTimeMilliseconds();
            }
            else
            {
              job.EndTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            job.State = MachineState.Idle;
            job.LastNotificationType = JobNotificationType.JobCompleted;
            await NotifyJobEvent(JobNotificationType.JobCompleted);
            break;

          case MachineState.Operational:
            job.State = MachineState.Operational;
            job.LastNotificationType = JobNotificationType.JobResumed;
            await NotifyJobEvent(JobNotificationType.JobResumed);
            break;

          case MachineState.Paused:
            job.State = MachineState.Paused;
            job.LastNotificationType = JobNotificationType.JobPaused;
            await NotifyJobEvent(JobNotificationType.JobPaused);
            break;
          default:
            Log.Warn($"Unhandled machine state transition: {job.State} -> {status.State} for machine {status.MachineId}");
            job.State = status.State;
            break;
        }

        job.LastStatus = status;
        job.LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _repository.Update(job);
      }
      catch (Exception ex)
      {
        Log.Error($"Machine Job Service Error processing machine {status?.MachineId}: {ex.Message}", ex);
      }
    }
  }
}
