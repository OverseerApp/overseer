using log4net;
using Overseer.Server.Models;

namespace Overseer.Server.Machines
{
  public abstract class MachineProvider<TMachine> : IMachineProvider<TMachine>, IDisposable
    where TMachine : Machine, new()
  {
    private bool _disposed;

    protected static readonly ILog Log = LogManager.GetLogger(typeof(MachineProvider<TMachine>));

    public int MachineId => Machine.Id;

    public abstract TMachine Machine { get; protected set; }

    public virtual Task PauseJob()
    {
      return ExecuteGcode("M25");
    }

    public virtual Task ResumeJob()
    {
      return ExecuteGcode("M24");
    }

    public virtual Task CancelJob()
    {
      return ExecuteGcode("M0");
    }

    public abstract Task ExecuteGcode(string command);

    public abstract Task LoadConfiguration(Machine machine);

    public abstract void Start(int interval);

    public abstract void Stop();

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (_disposed)
        return;

      if (disposing)
      {
        OnDisposing();
      }
      _disposed = true;
    }

    protected abstract void OnDisposing();

    ~MachineProvider()
    {
      Dispose(false);
    }
  }
}
