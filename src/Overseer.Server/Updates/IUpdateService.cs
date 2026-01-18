using Overseer.Server.Models;

namespace Overseer.Server.Updates
{
  public interface IUpdateService
  {
    /// <summary>
    /// Checks for available updates from GitHub releases
    /// </summary>
    /// <param name="includePreRelease">Include pre-release versions in the check</param>
    /// <returns>Update information including availability and version details</returns>
    Task<UpdateInfo> CheckForUpdatesAsync(bool includePreRelease = false);

    /// <summary>
    /// Initiates the update process on Linux systems with systemd service
    /// </summary>
    /// <param name="version">The version to update to</param>
    /// <returns>Result of the update initiation</returns>
    Task<UpdateResult> InitiateUpdateAsync(string version);

    /// <summary>
    /// Checks if the current platform supports auto-update
    /// </summary>
    /// <returns>True if auto-update is supported</returns>
    bool CanAutoUpdate();
  }
}
