using log4net;
using Overseer.Server.Automation;
using Overseer.Server.Channels;
using Overseer.Server.Models;

namespace Overseer.Server.Machines;

public class JobSentinel(
  Machine machine,
  MachineJob job,
  ICameraStreamer cameraStreamer,
  IFailureDetectionModel failureDetectionModel,
  Settings.IConfigurationManager configurationManager,
  IJobFailureChannel jobFailureChannel
) : IDisposable
{
  private static readonly ILog log = LogManager.GetLogger(typeof(JobSentinel));
  private readonly CancellationTokenSource _cancellationTokenSource = new();
  private Task? _monitoringTask;
  private bool _disposed;
  private readonly int _windowSize = 20;
  private readonly double _threshold = 0.7;
  private readonly Queue<JobFailureAnalysisResult> _history = new();

  private readonly Dictionary<string, float[]> _prototypes = PrototypeLoader.Load();

  public void StartMonitoring(CancellationToken externalCancellationToken)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (_monitoringTask != null)
      return;

    cameraStreamer.Start(machine.WebCamUrl!);
    _monitoringTask = MonitorJob(externalCancellationToken);
  }

  public async Task StopMonitoring()
  {
    if (_disposed)
      return;

    await _cancellationTokenSource.CancelAsync();
    cameraStreamer.Stop();
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

        var frame = cameraStreamer.GetProcessedFrame();
        if (frame == null || frame.Length == 0)
          continue;

        var result = AnalyzeFrame(frame);
        if (result is not null)
        {
          await jobFailureChannel.WriteAsync(result, stoppingToken);

          if (settings.AiMonitoringFailureAction != AIMonitoringFailureAction.CancelJob)
          {
            // if the print job doesn't get cancelled monitoring will continue after a delay
            // but that might get annoying if their is a partial failure and the user wants to
            // continue the job.
            // TODO: determine if this should support the user cancelling
            // monitoring but leaving the job running.
            _history.Clear();
            await Task.Delay(TimeSpan.FromMinutes(1), combinedToken);
            continue;
          }

          break;
        }
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

  /// <summary>
  /// Calculates the Euclidean Distance between two vectors.
  /// </summary>
  public double CalculateDistance(float[] vectorA, float[] vectorB)
  {
    if (vectorA.Length != vectorB.Length)
      throw new ArgumentException("Vectors must be the same length.");

    double sum = 0;
    for (int i = 0; i < vectorA.Length; i++)
    {
      double diff = vectorA[i] - vectorB[i];
      sum += diff * diff;
    }

    return Math.Sqrt(sum);
  }

  /// <summary>
  /// Determines if the current frame is a success or failure.
  /// </summary>
  private JobFailureAnalysisResult? AnalyzeFrame(float[] frame)
  {
    var currentEmbedding = failureDetectionModel.GetEmbedding(frame);
    string bestLabel = "Unknown";
    double shortestDistance = double.MaxValue;

    foreach (var proto in _prototypes)
    {
      double dist = CalculateDistance(currentEmbedding, proto.Value);
      if (dist < shortestDistance)
      {
        shortestDistance = dist;
        bestLabel = proto.Key;
      }
    }

    // Return both the specific type and a boolean flag for "Is this bad?"
    // Assuming any label that isn't "success" or "good" is a failure.
    bool isFailure = bestLabel.ToLower() != "success" && bestLabel.ToLower() != "good";
    var confidenceScore = 1.0 - (shortestDistance / 10.0); // Normalize distance to [0,1] for confidence
    var result = new JobFailureAnalysisResult
    {
      JobId = job.Id,
      IsFailureDetected = isFailure,
      ConfidenceScore = confidenceScore,
      FailureReason = isFailure ? bestLabel : "None",
      Details = $"Detected {bestLabel} with confidence of: {confidenceScore:F4}",
    };

    _history.Enqueue(result);

    if (_history.Count > _windowSize)
      _history.Dequeue();

    if (_history.Count < _windowSize)
      return null;

    int failureCount = _history.Count(x => x.IsFailureDetected);
    if ((double)failureCount / _history.Count >= _threshold)
    {
      var topFailure = _history
        .Where(x => x.IsFailureDetected)
        .GroupBy(x => x.FailureReason)
        .OrderByDescending(g => g.Count())
        .Select(g => g.FirstOrDefault())
        .FirstOrDefault();

      return topFailure;
    }

    return null;
  }
}
