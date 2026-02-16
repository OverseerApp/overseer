using System.Text.Json.Serialization;
using Overseer.Server.Integration.Machines;

namespace Overseer.Server.Models;

public class MachineMetadata
{
  public required string PropertyName { get; set; }

  public string? DisplayName { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public MachinePropertyDisplayType DisplayType { get; set; }

  public string? Description { get; set; }

  public bool IsRequired { get; set; }

  public bool IsSensitive { get; set; }

  public bool IsIgnored { get; set; }
}
