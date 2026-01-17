using log4net;
using Overseer.Server.Channels;
using Overseer.Server.Data;
using Overseer.Server.Machines;
using Overseer.Server.Models;

namespace Overseer.Server.Services;

public class JobFailureService(
  IDataContext dataContext,
  IControlManager controlManager,
  Settings.IConfigurationManager configurationManager,
  INotificationChannel notificationChannel,
  IJobFailureChannel jobFailureChannel
) : BackgroundService
{
  static readonly ILog Log = LogManager.GetLogger(typeof(JobFailureService));
  static readonly Guid _subscriberId = Guid.NewGuid();

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var jobs = dataContext.Repository<MachineJob>();
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var analysisResult = await jobFailureChannel.ReadAsync(_subscriberId, stoppingToken);
        if (analysisResult is null)
          continue;

        var job = jobs.GetById(analysisResult.JobId);
        if (job is null)
          continue;

        var notification = new JobFailureNotification
        {
          MachineId = job.MachineId,
          MachineJobId = analysisResult.JobId,
          Message = "job.aiDetectedFailure",
          AnalysisResult = analysisResult,
        };

        var settings = configurationManager.GetApplicationSettings();
        switch (settings.AiMonitoringFailureAction)
        {
          case AIMonitoringFailureAction.PauseJob:
            await controlManager.Pause(job.MachineId);
            notification.JobPaused = true;
            break;
          case AIMonitoringFailureAction.CancelJob:
            await controlManager.Cancel(job.MachineId);
            notification.JobCancelled = true;
            break;
        }

        await notificationChannel.WriteAsync(notification, stoppingToken);
      }
      catch (Exception ex)
      {
        Log.Error("Error processing job failure", ex);
      }
    }
  }
}
