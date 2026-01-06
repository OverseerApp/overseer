using log4net;
using Overseer.Server.Automation;
using Overseer.Server.Channels;
using Overseer.Server.Models;

namespace Overseer.Server.Machines;

public class JobSentinel(
  Machine machine,
  MachineJob job,
  IHttpClientFactory httpClientFactory,
  Settings.IConfigurationManager configurationManager,
  IJobFailureDetectionService failureDetectionService,
  IJobFailureChannel jobFailureChannel
) : IDisposable
{
  private static readonly ILog log = LogManager.GetLogger(typeof(JobSentinel));
  private readonly CancellationTokenSource _cancellationTokenSource = new();
  private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
  private Task? _monitoringTask;
  private bool _disposed;
  private byte[]? _lastCapturedImage;

  public void StartMonitoring(CancellationToken externalCancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (_monitoringTask != null)
      return;

    _monitoringTask = MonitorJob(externalCancellationToken);
  }

  public async Task StopMonitoring()
  {
    if (_disposed)
      return;

    await _cancellationTokenSource.CancelAsync();
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
      _httpClient.Dispose();
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
        var captureInterval = TimeSpan.FromMinutes(settings.AiMonitoringScanInterval);
        await Task.Delay(captureInterval, combinedToken);

        if (combinedToken.IsCancellationRequested)
          break;

        await CaptureAndAnalyzeImage(combinedToken);
        consecutiveFailures = 0; // Reset on success
      }
      catch (OperationCanceledException) when (combinedToken.IsCancellationRequested)
      {
        // Normal shutdown, no need to log as error
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

  private async Task CaptureAndAnalyzeImage(CancellationToken cancellationToken)
  {
    var currentImage = await CaptureWebcamImage(cancellationToken);
    if (currentImage == null)
    {
      log.Warn($"Failed to capture webcam image for machine {machine.Name}");
      return;
    }

    if (_lastCapturedImage is null)
    {
      _lastCapturedImage = currentImage;
      return;
    }

    // Analyze the images for job failure
    var analysisResult = await failureDetectionService.AnalyzeForJobFailureAsync(job.Id, _lastCapturedImage, currentImage);
    if (analysisResult.IsFailureDetected)
    {
      await jobFailureChannel.WriteAsync(analysisResult, cancellationToken);
    }
    _lastCapturedImage = currentImage;
  }

  private async Task<byte[]?> CaptureWebcamImage(CancellationToken cancellationToken)
  {
    try
    {
      if (string.IsNullOrEmpty(machine.SnapshotUrl))
      {
        log.Warn($"No SnapshotUrl configured for machine {machine.Name}");
        return null;
      }

      using var response = await _httpClient.GetAsync(machine.SnapshotUrl, cancellationToken);
      if (response.IsSuccessStatusCode)
      {
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
      }

      log.Warn($"Failed to capture webcam image. Status: {response.StatusCode}");
      return null;
    }
    catch (Exception ex)
    {
      log.Error($"Error capturing webcam image from {machine.SnapshotUrl}", ex);
      return null;
    }
  }
}
