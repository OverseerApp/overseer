using System.Text.Json.Serialization;

namespace Overseer.Server.Machines.Octoprint.Models;

public class Job
{
  [JsonPropertyName("job")]
  public JobDetails? JobDetails { get; set; }
  public Progress? Progress { get; set; }
  public string? State { get; set; }
}

public class JobDetails
{
  public FileDetails? File { get; set; }
  public double? EstimatedPrintTime { get; set; }
  public double? AveragePrintTime { get; set; }
  public Dictionary<string, Filament>? Filament { get; set; }
}

public class FileDetails
{
  public string? Name { get; set; }
  public string? Origin { get; set; }
  public int? Size { get; set; }
  public int? Date { get; set; }
}

public class Filament
{
  public double? Length { get; set; }
  public double? Volume { get; set; }
}

public class Progress
{
  public double? Completion { get; set; }
  public int? Filepos { get; set; }
  public int? PrintTime { get; set; }
  public int? PrintTimeLeft { get; set; }
}
