namespace Overseer.Server.Machines
{
  public interface IControlManager
  {
    Task Cancel(int machineId);
    Task Pause(int machineId);
    Task Resume(int machineId);
  }
}
