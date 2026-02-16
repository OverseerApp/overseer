using log4net;
using Overseer.Server.Channels;
using Overseer.Server.Integration.Machines;
using IConfigurationManager = Overseer.Server.Settings.IConfigurationManager;

namespace Overseer.Server.Machines;

public sealed class MonitoringService(
  IMachineManager machineManager,
  IConfigurationManager configurationManager,
  MachineProviderManager providerManager,
  IMachineStatusChannel machineStatusChannel
) : IDisposable, IMonitoringService
{
  static readonly ILog Log = LogManager.GetLogger(typeof(MonitoringService));

  public void StartMonitoring()
  {
    Log.Info("Starting monitoring service");
    var interval = configurationManager.GetApplicationSettings().Interval;
    var enabledMachines = machineManager.GetMachines().Where(m => !m.Disabled);
    foreach (var machine in enabledMachines)
    {
      var provider = providerManager.GetProvider(machine);
      provider.Start(interval, machine);
      provider.StatusUpdated += WriteStatusAsync;
    }
  }

  public void StopMonitoring()
  {
    Log.Info("Stopping monitoring service");
    var providers = providerManager.GetProviders();
    foreach (var provider in providers)
    {
      provider.StatusUpdated -= WriteStatusAsync;
      provider.Stop();
    }
  }

  public void RestartMonitoring()
  {
    Log.Info("Restarting monitoring service");
    StopMonitoring();
    StartMonitoring();
  }

  public void Dispose()
  {
    Log.Info("Disposing monitoring service");
    StopMonitoring();
  }

  async void WriteStatusAsync(object? sender, MachineStatusEventArgs eventArgs)
  {
    await machineStatusChannel.WriteAsync(eventArgs.Status);
  }
}
