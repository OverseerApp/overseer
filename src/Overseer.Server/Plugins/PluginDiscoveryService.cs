using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using log4net;
using Overseer.Server.Integration;

namespace Overseer.Server.Plugins;

public class PluginDiscoveryService()
{
  static readonly ILog Log = LogManager.GetLogger(typeof(PluginDiscoveryService));

  static readonly ConcurrentDictionary<string, PluginLoadContext> PluginLoadContexts = new();

  public static IEnumerable<IPluginConfiguration> DiscoverPlugins()
  {
    return PluginUtilities.GetPluginDirectories().Select(LoadPlugin).Where(pc => pc is not null).Cast<IPluginConfiguration>();
  }

  public static IPluginConfiguration? LoadPlugin(string pluginDirectoryPath)
  {
    try
    {
      var metadata = PluginUtilities.ReadPluginMetadata(pluginDirectoryPath);
      if (metadata == null)
      {
        // The uninstall will remove the metadata file if it exists
        // once the app is restarted it will remove any orphaned plugin directories
        Log.Warn($"No plugin metadata found in {pluginDirectoryPath}. Removing...");
        Directory.Delete(pluginDirectoryPath, true);
        return null;
      }

      var dllFiles = Directory.GetFiles(pluginDirectoryPath, "Overseer.*.dll").Where(f => !f.EndsWith("Overseer.Server.Integration.dll")).ToArray();

      if (dllFiles.Length == 0)
      {
        Log.Warn($"No 'Overseer' dll found in {pluginDirectoryPath}");
        return null;
      }

      if (dllFiles.Length > 1)
      {
        Log.Warn($"Ambiguous: Multiple 'Overseer' dlls found in {pluginDirectoryPath}");
        return null;
      }

      string mainDllPath = dllFiles.ElementAt(0);
      var loadContext = new PluginLoadContext(mainDllPath);
      Assembly assembly = loadContext.LoadFromAssemblyPath(mainDllPath);
      var pluginTypes = assembly.GetTypes().Where(t => typeof(IPluginConfiguration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
      if (!pluginTypes.Any())
      {
        Log.Warn($"No IPluginConfiguration implementation found in {mainDllPath}");
        return null;
      }

      if (pluginTypes.Count() > 1)
      {
        Log.Warn($"Multiple IPluginConfiguration implementations found in {mainDllPath}. Cannot determine which to use; no plugin will be loaded.");
        return null;
      }

      var type = pluginTypes.First();
      if (Activator.CreateInstance(type) is IPluginConfiguration pluginConfig)
      {
        Log.Info($"Discovered plugin configuration: {type.FullName} in {mainDllPath}");
        PluginLoadContexts.AddOrReplace(metadata.Name, loadContext);
        return pluginConfig;
      }
    }
    catch (Exception ex)
    {
      Log.Error($"Failed to load plugin from {pluginDirectoryPath}: {ex.Message}", ex);
    }

    return null;
  }
}
