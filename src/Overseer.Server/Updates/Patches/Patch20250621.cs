using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Updates.Patches;

class Patch20250621 : IPatch
{
  public Version Version { get; } = new Version(2, 0, 0, 0);

  public void Execute(LiteDataContext context)
  {
    var assemblyName = typeof(Patch20250621).Assembly.GetName().Name;
    var db = context.Database;
    var machineCollection = db.GetCollection(nameof(Machine));
    var machines = machineCollection.FindAll().ToList();
    foreach (var machine in machines)
    {
      var machineTypeValue = machine[nameof(Machine.MachineType)].AsString;
      if (!Enum.TryParse<MachineType>(machineTypeValue, ignoreCase: true, out var machineType))
        continue;

      var updateTypeName = machineType switch
      {
        MachineType.RepRapFirmware => typeof(RepRapFirmwareMachine).FullName,
        MachineType.Octoprint => typeof(OctoprintMachine).FullName,
        MachineType.Elegoo => typeof(ElegooMachine).FullName,
        MachineType.Bambu => typeof(BambuMachine).FullName,
        MachineType.Moonraker => typeof(MoonrakerMachine).FullName,
        MachineType.DuetSoftwareFramework => typeof(DuetSoftwareFrameworkMachine).FullName,
        _ => null,
      };

      if (updateTypeName == null)
        continue;

      machine["_type"] = $"{updateTypeName}, {assemblyName}";
      machineCollection.Update(machine);
    }

    var valueStoreCollection = db.GetCollection(nameof(LiteValueStore.ValueRecord));
    var settingsValueRecord = valueStoreCollection.FindById(nameof(ApplicationSettings));
    if (settingsValueRecord != null)
    {
      settingsValueRecord["Value"]["_type"] = $"{typeof(ApplicationSettings).FullName}, {assemblyName}";
      valueStoreCollection.Update(settingsValueRecord);
    }
  }
}
