using Overseer.Server.Models;
using Overseer.Server.Plugins;
using Overseer.Server.Plugins.Models;

namespace Overseer.Server.Api;

public static class PluginsApi
{
  public static RouteGroupBuilder MapPluginsApi(this RouteGroupBuilder builder)
  {
    var group = builder.MapGroup("plugins");
    group.RequireAuthorization(AccessLevel.Administrator.ToString());

    group.MapGet("/", (IPluginManager pluginManager) => pluginManager.GetRegistryItems()).CacheOutput("RefreshCache");

    group.MapPost("/", async (PluginRegistryItem item, IPluginManager pluginManager) => await pluginManager.InstallPlugin(item));

    group.MapDelete("/{pluginName}", (string pluginName, IPluginManager pluginManager) => pluginManager.UninstallPlugin(pluginName));

    return builder;
  }
}
