using Overseer.Server.Channels;
using Overseer.Server.Data;
using Overseer.Server.Integration.Machines;
using Overseer.Server.Models;

namespace Overseer.Server.Machines;

public class MachineManager(IDataContext context, MachineProviderManager machineProviderManager, IRestartMonitoringChannel restartMonitoringChannel)
  : IMachineManager
{
  readonly IRepository<Machine> _machines = context.Repository<Machine>();
  readonly MachineProviderManager _machineProviderManager = machineProviderManager;

  public Machine GetMachine(int id)
  {
    return _machines.GetById(id);
  }

  public IReadOnlyList<Machine> GetMachines()
  {
    var machines = _machines.GetAll();
    return machines;
  }

  public async Task<Machine> CreateMachine(Machine machine)
  {
    //load any default configuration that will be retrieved from the machine.
    var configuredMachine = await _machineProviderManager.ConfigureMachine(machine);

    //The new machine will be added to the end of the list
    configuredMachine.SortIndex = _machines.Count() + 1;

    //if the configuration is updated with data from the machine then store the configuration.
    _machines.Create(configuredMachine);
    await restartMonitoringChannel.Dispatch();

    return configuredMachine;
  }

  public async Task<Machine> UpdateMachine(Machine machine)
  {
    if (!machine.Disabled)
    {
      //update the configuration from the machine if the machine isn't disabled
      var configuredMachine = await _machineProviderManager.ConfigureMachine(machine);
      _machines.Update(configuredMachine);

      await restartMonitoringChannel.Dispatch();
      return configuredMachine;
    }

    _machines.Update(machine);
    return machine;
  }

  public Machine? DeleteMachine(int machineId)
  {
    var machine = GetMachine(machineId);
    if (machine == null)
      return null;

    _machines.Delete(machineId);
    return machine;
  }

  public void SortMachines(List<int> sortOrder)
  {
    var machines = _machines.GetAll().ToList();
    machines.ForEach(m => m.SortIndex = sortOrder.IndexOf(m.Id));

    _machines.Update(machines);
  }

  public IDictionary<string, IEnumerable<MachineMetadata>> GetMachineMetadata()
  {
    return _machineProviderManager.GetMachineMetadata();
  }
}
