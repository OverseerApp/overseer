namespace Overseer.Server.Machines;

public class ControlManager(IMachineManager machineManager, MachineProviderManager machineProviderManager) : IControlManager
{
  public Task Pause(int machineId)
  {
    return machineProviderManager.GetProvider(machineManager.GetMachine(machineId)).PauseJob();
  }

  public Task Resume(int machineId)
  {
    return machineProviderManager.GetProvider(machineManager.GetMachine(machineId)).ResumeJob();
  }

  public Task Cancel(int machineId)
  {
    return machineProviderManager.GetProvider(machineManager.GetMachine(machineId)).CancelJob();
  }
}
