using System.Collections.Concurrent;
using log4net;
using Overseer.Server.Channels;
using Overseer.Server.Data;
using Overseer.Server.Machines;
using Overseer.Server.Models;

namespace Overseer.Server.Services;

public sealed class JobSentinelService(
  IDataContext dataContext,
  INotificationChannel notificationChannel,
  Settings.IConfigurationManager configurationManager,
  Func<Machine, MachineJob, JobSentinel> createSentinel
) : BackgroundService, IAsyncDisposable
{
  private static readonly ILog log = LogManager.GetLogger(typeof(JobSentinelService));
  private readonly Guid _subscriberId = Guid.NewGuid();
  private readonly ConcurrentDictionary<int, JobSentinel> _activeSentinels = new();
  private readonly IRepository<MachineJob> _jobRepository = dataContext.Repository<MachineJob>();

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // come back to this, the concern is that if monitoring is disabled/enabled while service is running
    var settings = configurationManager.GetApplicationSettings();
    if (settings.EnableAiMonitoring)
    {
      StartJobSentinels(stoppingToken);
    }

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var notification = await notificationChannel.ReadAsync(_subscriberId, stoppingToken);
        var latestSettings = configurationManager.GetApplicationSettings();
        if (settings.EnableAiMonitoring != latestSettings.EnableAiMonitoring)
        {
          if (latestSettings.EnableAiMonitoring)
          {
            StartJobSentinels(stoppingToken);
          }
          else
          {
            await StopAllSentinels();
          }
          settings = latestSettings;
          // should be able to just continue here because the sentinel start/stop logic above handles the state change
          continue;
        }

        // if monitoring is disabled, skip processing notifications
        if (!settings.EnableAiMonitoring)
          continue;

        if (notification is not JobNotification jobNotification)
          continue;

        switch (jobNotification.Type)
        {
          case JobNotificationType.JobStarted:
            var job = _jobRepository.GetById(jobNotification.MachineJobId);
            if (job != null)
            {
              StartJobSentinel(job, stoppingToken);
            }
            break;
          case JobNotificationType.JobCompleted:
            await StopJobSentinel(jobNotification.MachineJobId);
            break;
        }
      }
      catch (Exception ex)
      {
        log.Error("Error processing job notification in JobSentinelService", ex);
      }
    }

    await StopAllSentinels();
  }

  private void StartJobSentinels(CancellationToken stoppingToken)
  {
    try
    {
      var activeJobs = _jobRepository.Filter(x => !x.EndTime.HasValue);
      foreach (var job in activeJobs)
      {
        StartJobSentinel(job, stoppingToken);
      }
    }
    catch (Exception ex)
    {
      log.Error("Error initializing active sentinels", ex);
    }
  }

  private void StartJobSentinel(MachineJob job, CancellationToken stoppingToken)
  {
    var machineRepository = dataContext.Repository<Machine>();
    var machine = machineRepository.GetById(job.MachineId);
    if (string.IsNullOrEmpty(machine?.SnapshotUrl))
    {
      log.Warn($"Machine {machine?.Name} does not have a valid Webcam URL. Sentinel not created for job {job.Id}");
      return;
    }

    var sentinel = createSentinel(machine, job);
    if (!_activeSentinels.TryAdd(job.Id, sentinel))
    {
      log.Warn($"Sentinel already exists for job {job.Id}");
      sentinel.Dispose(); // Clean up the unused sentinel
      return;
    }

    sentinel.StartMonitoring(stoppingToken);
    log.Info($"Started job sentinel for job {job.Id} on machine {machine.Name}");
  }

  private async Task StopJobSentinel(int jobId)
  {
    if (_activeSentinels.TryRemove(jobId, out var sentinel))
    {
      await sentinel.StopMonitoring();
      sentinel.Dispose();
      log.Info($"Stopped job sentinel for job {jobId}");
    }
  }

  private async Task StopAllSentinels()
  {
    var sentinelIds = _activeSentinels.Keys.ToList();
    foreach (var jobId in sentinelIds)
    {
      await StopJobSentinel(jobId);
    }
  }

  public async ValueTask DisposeAsync()
  {
    await StopAllSentinels();
    GC.SuppressFinalize(this);
  }
}
