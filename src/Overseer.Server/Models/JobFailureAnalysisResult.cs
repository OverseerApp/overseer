using Overseer.Server.Integration.Automation;

namespace Overseer.Server.Models;

public record JobFailureAnalysisResult : FailureDetectionAnalysisResult
{
  public JobFailureAnalysisResult()
    : base() { }

  public JobFailureAnalysisResult(FailureDetectionAnalysisResult result)
    : this()
  {
    ConfidenceScore = result.ConfidenceScore;
    Details = result.Details;
    FailureReason = result.FailureReason;
    IsFailureDetected = result.IsFailureDetected;
  }

  public int JobId { get; set; }
}
