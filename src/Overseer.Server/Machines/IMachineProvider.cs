using Overseer.Server.Models;

namespace Overseer.Server.Machines
{
  public interface IMachineProvider
  {
    int MachineId { get; }

    Task PauseJob();

    Task ResumeJob();

    Task CancelJob();

    Task ExecuteGcode(string command);

    Task LoadConfiguration(Machine machine);

    void Start(int interval);

    void Stop();
  }

  public interface IMachineProvider<out TMachine> : IMachineProvider
    where TMachine : Machine, new()
  {
    TMachine Machine { get; }
  }
}
