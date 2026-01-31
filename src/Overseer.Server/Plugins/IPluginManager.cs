using Overseer.Server.Plugins.Models;

namespace Overseer.Server.Plugins;

public interface IPluginManager
{
  Task<IEnumerable<PluginRegistryItem>> GetRegistryItems();
  IEnumerable<PluginRegistryItem> GetInstalledPlugins();
  Task<bool> InstallPlugin(PluginRegistryItem item);
  bool UninstallPlugin(string pluginName);
}
