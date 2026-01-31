using Overseer.Server.Models;

namespace Overseer.Server.System
{
  public interface ISystemManager
  {
    /// <summary>
    /// Checks for available updates from GitHub releases
    /// </summary>
    /// <returns>Update information including availability and version details</returns>
    Task<UpdateInfo> CheckForUpdates();

    /// <summary>
    /// Initiates the update process on Linux systems with systemd service
    /// </summary>
    /// <param name="version">The version to update to</param>
    /// <returns>Result of the update initiation</returns>
    void InitiateUpdate(string version);

    /// <summary>
    /// Checks if the current platform supports auto-update
    /// </summary>
    /// <returns>True if auto-update is supported</returns>
    bool CanAutoUpdate();

    /// <summary>
    /// Restarts the overseer application/service
    /// </summary>
    void InitiateRestart();
  }
}
