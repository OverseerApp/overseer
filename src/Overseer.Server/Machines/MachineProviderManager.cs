using System.Collections.Concurrent;
using Overseer.Server.Integration.Machines;
using Overseer.Server.Models;

namespace Overseer.Server.Machines;

public class MachineProviderManager(Func<Machine, IMachineProvider> providerFactory)
{
  static readonly ConcurrentDictionary<int, IMachineProvider> _providerCache = new();

  static readonly Lazy<ConcurrentDictionary<Type, Type>> _machineToProviderTypeMap = new(() =>
  {
    var mapping = typeof(IMachineProvider)
      .GetAssignableTypes()
      .ToDictionary(type =>
      {
        return type.GetInterfaces().SelectMany(i => i.GetGenericArguments()).First(arg => typeof(Machine).IsAssignableFrom(arg));
      });

    return new ConcurrentDictionary<Type, Type>(mapping);
  });

  public IEnumerable<IMachineProvider> GetProviders(IEnumerable<Machine> machines)
  {
    return machines.Where(machine => !machine.Disabled).Select(GetProvider);
  }

  public IMachineProvider CreateProvider(Machine machine)
  {
    return providerFactory.Invoke(machine);
  }

  public IMachineProvider GetProvider(Machine machine)
  {
    return _providerCache.GetOrAdd(machine.Id, id => CreateProvider(machine));
  }

  public static Type GetProviderType(Machine machine)
  {
    if (_machineToProviderTypeMap.Value.TryGetValue(machine.GetType(), out Type? providerType) && providerType != null)
      return providerType;

    throw new InvalidOperationException("Unmapped Machine Type");
  }
}
