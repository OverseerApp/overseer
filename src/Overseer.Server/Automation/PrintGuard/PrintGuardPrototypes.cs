using System.Reflection;
using System.Text.Json;

namespace Overseer.Server.Automation.PrintGuard;

public static class PrintGuardPrototypes
{
  private static readonly Lazy<Dictionary<string, float[]>> _prototypes = new(() =>
  {
    var prototypesJson = LoadEmbeddedResource("Overseer.Server.Resources.print_guard_prototypes.json");
    var prototypesDict = JsonSerializer.Deserialize<Dictionary<string, float[]>>(prototypesJson);
    return prototypesDict ?? [];
  });

  public static Dictionary<string, float[]> Get() => _prototypes.Value;

  private static byte[] LoadEmbeddedResource(string resourceName)
  {
    var assembly = Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream(resourceName);

    if (stream == null)
    {
      var availableResources = assembly.GetManifestResourceNames();
      throw new InvalidOperationException(
        $"Embedded resource '{resourceName}' not found. Available resources: {string.Join(", ", availableResources)}"
      );
    }

    using var memoryStream = new MemoryStream();
    stream.CopyTo(memoryStream);
    return memoryStream.ToArray();
  }
}
