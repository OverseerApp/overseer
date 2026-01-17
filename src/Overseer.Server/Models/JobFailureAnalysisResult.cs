namespace Overseer.Server.Models;

public record JobFailureAnalysisResult
{
  public int JobId { get; set; }
  public bool IsFailureDetected { get; set; }
  public double ConfidenceScore { get; set; }
  public string? FailureReason { get; set; }
  public string? Details { get; set; }
}
