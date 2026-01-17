using System.Reflection;
using System.Text.Json;

namespace Overseer.Server.Automation;

public static class PrototypeLoader
{
  public static Dictionary<string, float[]> Load()
  {
    var prototypesJson = LoadEmbeddedResource("Overseer.Server.Resources.prototypes.json");
    var prototypesDict = JsonSerializer.Deserialize<Dictionary<string, float[]>>(prototypesJson);
    return prototypesDict ?? [];
  }

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
