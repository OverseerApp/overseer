using System.Reflection;
using System.Text.Json;
using log4net;
using Overseer.Server.Plugins.Models;

namespace Overseer.Server.Plugins;

public static class PluginUtilities
{
  static readonly ILog Log = LogManager.GetLogger(typeof(PluginUtilities));

  public static string GetPluginsPath()
  {
    var executingAssembly = Assembly.GetExecutingAssembly();
    var basePath = Path.GetDirectoryName(executingAssembly.Location);
    if (basePath == null)
    {
      Log.Error("Failed to determine executing assembly path.");
      return string.Empty;
    }

    return Path.Combine(basePath, "Plugins");
  }

  public static string[] GetPluginDirectories()
  {
    var pluginsPath = GetPluginsPath();
    if (!Directory.Exists(pluginsPath))
    {
      Log.Warn("Plugins directory does not exist.");
      return [];
    }

    return Directory.GetDirectories(pluginsPath);
  }

  public static (string Owner, string Name) ParseGitHubUrl(string url)
  {
    var uri = new Uri(url.TrimEnd('/'));
    var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length < 2)
      throw new ArgumentException("Invalid GitHub URL.");

    string owner = parts[0];
    string name = parts[1].Replace(".git", ""); // Remove .git if present

    return (owner, name);
  }

  public static PluginRegistryItem? ReadPluginMetadata(string pluginDirectory)
  {
    var metadataPath = Path.Combine(pluginDirectory, "plugin.json");
    if (File.Exists(metadataPath))
    {
      try
      {
        var metadataJson = File.ReadAllText(metadataPath);
        var item = JsonSerializer.Deserialize<PluginRegistryItem>(metadataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (item != null)
        {
          return item;
        }
      }
      catch (Exception ex)
      {
        Log.Error($"Failed to read plugin metadata from {metadataPath}: {ex.Message}", ex);
      }
    }

    return null;
  }
}
