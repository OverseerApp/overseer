using System.Reflection;
using System.Runtime.Loader;
using log4net;

namespace Overseer.Server.Plugins;

public class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
  static readonly ILog Log = LogManager.GetLogger(typeof(PluginLoadContext));

  private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

  protected override Assembly? Load(AssemblyName assemblyName)
  {
    if (assemblyName?.Name == null)
      return null;

    var appAssemblies = GetAppAssemblies();
    if (appAssemblies.Contains(assemblyName.Name))
    {
      // If the requested assembly is already loaded in the default context use it
      return null;
    }

    // 1. Try to resolve the path using the .deps.json file
    var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
    if (!string.IsNullOrWhiteSpace(assemblyPath))
    {
      return LoadFromAssemblyPath(assemblyPath);
    }

    // 2. If not found in .deps.json, manually check the plugin directory
    // This handles cases where dependencies are just copied into the folder
    var pluginDirectory = Path.GetDirectoryName(pluginPath);
    if (pluginDirectory == null)
      return null;

    var manualPath = Path.Combine(pluginDirectory, $"{assemblyName.Name}.dll");
    if (File.Exists(manualPath))
    {
      return LoadFromAssemblyPath(manualPath);
    }

    // 3. Return null to let the Default Context try to load it.
    // This is CRITICAL for shared libraries (like your IPlugin interface)
    // and system runtime libraries.
    return null;
  }

  protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
  {
    // Resolve native/unmanaged libraries if your plugin uses them
    var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
    if (!string.IsNullOrWhiteSpace(libraryPath))
    {
      return LoadUnmanagedDllFromPath(libraryPath);
    }

    Log.Warn($"Unmanaged DLL '{unmanagedDllName}' could not be resolved for plugin at '{pluginPath}'.");
    return IntPtr.Zero;
  }

  private static List<string> GetAppAssemblies()
  {
    return
    [
      .. AppDomain
        .CurrentDomain.GetAssemblies()
        .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
        .Select(a => Path.GetFileNameWithoutExtension(a.Location) ?? string.Empty),
    ];
  }
}
