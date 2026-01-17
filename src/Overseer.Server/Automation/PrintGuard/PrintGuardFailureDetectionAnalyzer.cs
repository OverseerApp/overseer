using Overseer.Server.Models;

namespace Overseer.Server.Automation.PrintGuard;

public class PrintGuardFailureDetectionAnalyzer(PrintGuardModel model, IPrintGuardCameraStreamer cameraStreamer) : IFailureDetectionAnalyzer
{
  private readonly int _windowSize = 20;
  private readonly double _threshold = 0.7;
  private readonly Queue<JobFailureAnalysisResult> _history = new();

  public JobFailureAnalysisResult Analyze()
  {
    var frame = cameraStreamer.GetProcessedFrame();
    if (frame == null || frame.Length == 0)
      throw new InvalidOperationException("No frame data available from camera streamer.");

    return AnalyzeFrame(frame);
  }

  /// <summary>
  /// Calculates the Euclidean Distance between two vectors.
  /// </summary>
  private static double CalculateDistance(float[] vectorA, float[] vectorB)
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
  private JobFailureAnalysisResult AnalyzeFrame(float[] frame)
  {
    var currentEmbedding = model.GetEmbedding(frame);
    string bestLabel = "Undetermined";
    double shortestDistance = double.MaxValue;

    foreach (var proto in PrintGuardPrototypes.Get())
    {
      double dist = CalculateDistance(currentEmbedding, proto.Value);
      if (dist < shortestDistance)
      {
        shortestDistance = dist;
        bestLabel = proto.Key;
      }
    }

    bool isFailure = !bestLabel.Equals("success", StringComparison.CurrentCultureIgnoreCase);
    // Normalize distance to [0,1] for confidence
    var confidenceScore = Math.Clamp(1.0 - (shortestDistance / 10.0), 0.0, 1.0);
    var result = new JobFailureAnalysisResult
    {
      IsFailureDetected = isFailure,
      ConfidenceScore = confidenceScore,
      FailureReason = isFailure ? bestLabel : "None",
      Details = $"Detected {bestLabel} with confidence of: {confidenceScore:F4}",
    };

    _history.Enqueue(result);

    if (_history.Count > _windowSize)
    {
      _history.Dequeue();
    }

    if (_history.Count < _windowSize)
    {
      return new JobFailureAnalysisResult
      {
        IsFailureDetected = false,
        ConfidenceScore = 1.0,
        FailureReason = "Insufficient Data",
        Details = "Not enough data collected to determine failure.",
      };
    }

    int failureCount = _history.Count(x => x.IsFailureDetected);
    if ((double)failureCount / _history.Count >= _threshold)
    {
      var topFailure = _history
        .Where(x => x.IsFailureDetected)
        .GroupBy(x => x.FailureReason)
        .OrderByDescending(g => g.Count())
        .Select(g => g.FirstOrDefault())
        .FirstOrDefault();

      _history.Clear();

      return topFailure
        ?? new JobFailureAnalysisResult
        {
          IsFailureDetected = true,
          ConfidenceScore = 1.0,
          FailureReason = "Undetermined",
          Details = "Failure detected but unable to determine reason.",
        };
    }

    return new JobFailureAnalysisResult
    {
      IsFailureDetected = false,
      ConfidenceScore = 1.0,
      FailureReason = "No Failure",
      Details = "No failure detected based on recent analysis.",
    };
  }

  public void Start(string url)
  {
    cameraStreamer.Start(url);
  }

  public void Stop()
  {
    cameraStreamer.Stop();
  }
}
