using Overseer.Server.Models;

namespace Overseer.Server.Automation;

/// <summary>
/// Interface for analyzing failure detection from camera frames.
/// </summary>
public interface IFailureDetectionAnalyzer
{
  /// <summary>
  /// Starts capturing frames from the given URL for analysis.
  /// </summary>
  void Start(string url);

  /// <summary>
  /// Stops the frame capturing process.
  /// </summary>
  void Stop();

  /// <summary>
  /// Analyzes the latest frame for failure detection.
  /// </summary>
  JobFailureAnalysisResult Analyze();
}
