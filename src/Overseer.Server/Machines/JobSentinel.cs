using log4net;
using Overseer.Server.Automation;
using Overseer.Server.Channels;
using Overseer.Server.Models;

namespace Overseer.Server.Machines;

public class JobSentinel(
  Machine machine,
  MachineJob job,
  IFailureDetectionAnalyzer failureDetectionAnalyzer,
  Settings.IConfigurationManager configurationManager,
  IJobFailureChannel jobFailureChannel
) : IDisposable
{
  private static readonly ILog log = LogManager.GetLogger(typeof(JobSentinel));
  private readonly CancellationTokenSource _cancellationTokenSource = new();
  private Task? _monitoringTask;
  private bool _disposed;

  public void StartMonitoring(CancellationToken externalCancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (_monitoringTask != null)
      return;

    failureDetectionAnalyzer.Start(machine.WebCamUrl!);
    _monitoringTask = MonitorJob(externalCancellationToken);
  }

  public async Task StopMonitoring()
  {
    if (_disposed)
      return;

    await _cancellationTokenSource.CancelAsync();
    failureDetectionAnalyzer.Stop();
    if (_monitoringTask != null)
    {
      await _monitoringTask;
    }
  }

  public void Dispose()
  {
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (_disposed)
      return;

    if (disposing)
    {
      _cancellationTokenSource.Cancel();
      _cancellationTokenSource.Dispose();
    }

    _disposed = true;
  }

  private async Task MonitorJob(CancellationToken stoppingToken)
  {
    const int initialBackoffSeconds = 30;
    const int maxConsecutiveFailures = 3;
    var consecutiveFailures = 0;

    using var linkedTokens = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _cancellationTokenSource.Token);
    var combinedToken = linkedTokens.Token;

    while (!combinedToken.IsCancellationRequested)
    {
      try
      {
        var settings = configurationManager.GetApplicationSettings();
        var captureInterval = TimeSpan.FromMilliseconds(1000.0 / settings.AiMonitoringFrameCaptureRate);
        await Task.Delay(captureInterval, combinedToken);

        var result = failureDetectionAnalyzer.Analyze();
        if (result is null || !result.IsFailureDetected)
          continue;

        await jobFailureChannel.WriteAsync(result, stoppingToken);

        if (settings.AiMonitoringFailureAction != AIMonitoringFailureAction.CancelJob)
        {
          // if the print job doesn't get cancelled monitoring will continue after a delay
          // but that might get annoying if their is a partial failure and the user wants to
          // continue the job.
          // TODO: determine if this should support the user cancelling
          // monitoring but leaving the job running.
          failureDetectionAnalyzer.Stop();
          await Task.Delay(TimeSpan.FromMinutes(1), combinedToken);
          failureDetectionAnalyzer.Start(machine.WebCamUrl!);
          continue;
        }

        break;
      }
      catch (Exception ex)
      {
        consecutiveFailures++;
        log.Error($"Error monitoring job {job.Id} (failure {consecutiveFailures}/{maxConsecutiveFailures})", ex);

        if (consecutiveFailures >= maxConsecutiveFailures)
        {
          throw new InvalidOperationException($"Job monitoring for job {job.Id} failed {maxConsecutiveFailures} consecutive times", ex);
        }

        // Backoff before retrying
        var backoffDelay = TimeSpan.FromSeconds(initialBackoffSeconds * consecutiveFailures);
        log.Info($"Backing off for {backoffDelay.TotalSeconds} seconds before retrying");
        await Task.Delay(backoffDelay, combinedToken);
      }
    }
  }
}
