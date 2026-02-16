using System.Collections.Concurrent;
using System.Reflection;
using Overseer.Server.Integration.Machines;
using Overseer.Server.Models;
using Overseer.Server.Plugins;

namespace Overseer.Server.Machines;

public class MachineProviderManager(IServiceProvider serviceProvider, IDictionary<Type, Type> providerTypeMap, IList<Type> configurationProviderTypes)
{
  static readonly Lazy<IDictionary<string, IEnumerable<MachineMetadata>>> _machineMetadataCache = new(DiscoverMachineMetadata);

  static readonly ConcurrentDictionary<int, IMachineProvider> _providerCache = new();

  static readonly ConcurrentDictionary<string, Type> _providerTypeCache = new();

  public IDictionary<string, IEnumerable<MachineMetadata>> GetMachineMetadata()
  {
    return _machineMetadataCache.Value;
  }

  public async Task<Machine> ConfigureMachine(Machine machine)
  {
    if (string.IsNullOrWhiteSpace(machine.MachineType))
      throw new InvalidOperationException("Machine type must be specified");

    var machineType = DiscoverMachineType(machine.MachineType);
    var configProviderType = configurationProviderTypes.FirstOrDefault(t => t.GetGenericArguments()[0] == machineType);

    if (configProviderType == null)
      throw new InvalidOperationException($"No configuration provider found for machine type {machineType.Name}");

    var configProviderInstance = serviceProvider.GetService(configProviderType);
    var task = (Task)configProviderType.GetMethod("Configure")!.Invoke(configProviderInstance, [machine])!;
    await task.ConfigureAwait(false);
    var result = task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task);
    return (Machine)result!;
  }

  public IMachineProvider CreateProvider(Machine machine)
  {
    if (string.IsNullOrWhiteSpace(machine.MachineType))
      throw new InvalidOperationException("Machine type must be specified");

    if (_providerTypeCache.TryGetValue(machine.MachineType, out var cachedProviderType))
    {
      var cachedProviderInstance = serviceProvider.GetService(cachedProviderType);
      if (cachedProviderInstance is IMachineProvider cachedProvider)
        return cachedProvider;
    }

    var machineType = DiscoverMachineType(machine.MachineType);
    if (!providerTypeMap.TryGetValue(machineType, out var providerType))
      throw new InvalidOperationException($"No provider registered for machine type {machine.MachineType}");

    var providerInstance = serviceProvider.GetService(providerType);
    if (providerInstance is not IMachineProvider provider)
      throw new InvalidOperationException($"Provider for machine type {machine.MachineType} could not be instantiated");

    _providerTypeCache[machine.MachineType] = providerType;
    return provider;
  }

  public IEnumerable<IMachineProvider> GetProviders()
  {
    return _providerCache.Values;
  }

  public IMachineProvider GetProvider(Machine machine)
  {
    return _providerCache.GetOrAdd(machine.Id, id => CreateProvider(machine));
  }

  private static Type DiscoverMachineType(string machineTypeName)
  {
    var machineType = PluginDiscoveryService
      .FindTypes(t =>
      {
        if (!t.IsClass || t.IsAbstract)
          return false;

        var attr = t.GetCustomAttribute<MachineTypeAttribute>();
        if (attr == null)
          return false;

        return attr.Name == machineTypeName;
      })
      .FirstOrDefault();

    if (machineType == null)
      throw new InvalidOperationException($"Machine type couldn't be found for machine type {machineTypeName}");

    return machineType;
  }

  private static Dictionary<string, IEnumerable<MachineMetadata>> DiscoverMachineMetadata()
  {
    var result = new Dictionary<string, IEnumerable<MachineMetadata>>();

    var machineTypes = PluginDiscoveryService.FindTypes(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<MachineTypeAttribute>() != null);
    foreach (var type in machineTypes)
    {
      var machineTypeAttr = type.GetCustomAttribute<MachineTypeAttribute>()!;
      var metadata = type.GetProperties()
        .Select(p => new { Property = p, Attribute = p.GetCustomAttribute<MachinePropertyAttribute>() })
        .Where(x => x.Attribute != null && !x.Attribute.IsIgnored)
        .Select(x => new MachineMetadata
        {
          PropertyName = x.Property.Name,
          DisplayName = x.Attribute!.DisplayName,
          DisplayType = x.Attribute.DisplayType,
          Description = x.Attribute.Description,
          IsRequired = x.Attribute.IsRequired,
          IsSensitive = x.Attribute.IsSensitive,
          IsIgnored = x.Attribute.IsIgnored,
        });

      result[machineTypeAttr.Name] = [.. metadata];
    }

    return result;
  }
}
