using System.Diagnostics;
using System.Runtime.InteropServices;
using log4net;
using Octokit;
using Overseer.Server.Models;

namespace Overseer.Server.System
{
  public class SystemManager(IWebHostEnvironment environment, IGitHubClient gitHubClient) : ISystemManager
  {
    static readonly ILog Log = LogManager.GetLogger(typeof(SystemManager));

    const string OverseerScriptName = "overseer.sh";

    readonly IWebHostEnvironment _environment = environment;

    readonly IGitHubClient _gitHubClient = gitHubClient;

    public async Task<UpdateInfo> CheckForUpdates()
    {
      var currentVersion = ApplicationInfo.Instance.Version;
      var updateInfo = new UpdateInfo
      {
        CurrentVersion = currentVersion,
        UpdateAvailable = false,
        CanAutoUpdate = CanAutoUpdate(),
      };

      try
      {
        // Find the latest applicable release
        var latestRelease = await _gitHubClient.Repository.Release.GetLatest("OverseerApp", "overseer");
        if (latestRelease == null)
        {
          Log.Info("No applicable releases found");
          return updateInfo;
        }

        var latestVersion = NormalizeVersion(latestRelease.TagName);
        var currentVersionNormalized = NormalizeVersion(currentVersion);

        updateInfo.LatestVersion = latestRelease.TagName;
        updateInfo.ReleaseUrl = latestRelease.HtmlUrl;
        updateInfo.ReleaseNotes = latestRelease.Body;
        updateInfo.PublishedAt = latestRelease.PublishedAt?.UtcDateTime;
        updateInfo.IsPreRelease = latestRelease.Prerelease;

        // Find the download URL for the server package
        var serverAsset = latestRelease.Assets?.FirstOrDefault(a =>
          a.Name != null && a.Name.StartsWith("overseer_server_") && a.Name.EndsWith(".zip")
        );

        if (serverAsset != null)
        {
          updateInfo.DownloadUrl = serverAsset.BrowserDownloadUrl;
        }

        // Compare versions
        if (TryCompareVersions(latestVersion, currentVersionNormalized, out var comparison))
        {
          updateInfo.UpdateAvailable = comparison > 0;
        }
        else
        {
          // Fall back to string comparison for non-standard versions
          updateInfo.UpdateAvailable = !string.Equals(latestVersion, currentVersionNormalized, StringComparison.OrdinalIgnoreCase);
        }

        Log.Info(
          $"Update check complete. Current: {currentVersion}, Latest: {latestRelease.TagName}, Update Available: {updateInfo.UpdateAvailable}"
        );
      }
      catch (Exception ex)
      {
        Log.Error("Error checking for updates", ex);
      }

      return updateInfo;
    }

    public void InitiateUpdate(string version)
    {
      InvokeOverseerScript("update", version, $"\"{_environment.ContentRootPath}\"", $"\"{GetDotNetPath()}\"");
    }

    public void InitiateRestart()
    {
      InvokeOverseerScript("restart");
    }

    public bool CanAutoUpdate()
    {
      // Auto-update is only supported on Linux with systemd
      return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && ServiceExists();
    }

    private static bool ServiceExists()
    {
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        return false;
      }

      var servicePath = "/lib/systemd/system/overseer.service";
      return File.Exists(servicePath);
    }

    private string? FindOverseerScript()
    {
      // Look for the updater script in several locations
      var searchPaths = new[]
      {
        // Docker installation: /opt/overseer/overseer.sh
        Path.Combine(_environment.ContentRootPath, "..", OverseerScriptName),
        // Manual installation (scripts folder adjacent to app): ./scripts/overseer.sh
        Path.Combine(_environment.ContentRootPath, "scripts", OverseerScriptName),
        Path.Combine(_environment.ContentRootPath, "..", "scripts", OverseerScriptName),
        // Absolute path for Docker: /opt/overseer/overseer.sh
        Path.Combine("/opt/overseer", OverseerScriptName),
        // Legacy path (keeping for backward compatibility)
        Path.Combine("/opt/overseer/scripts", OverseerScriptName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "overseer/scripts", OverseerScriptName),
      };

      return searchPaths.FirstOrDefault(p => File.Exists(p));
    }

    private string GetDotNetPath()
    {
      // Try to determine the .NET path
      var possiblePaths = new[]
      {
        // Docker installation: /opt/overseer/.dotnet
        Path.Combine(_environment.ContentRootPath, "..", ".dotnet"),
        // Manual installation relative to current directory
        Path.Combine(Directory.GetCurrentDirectory(), ".dotnet"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
        // Absolute path for Docker
        "/opt/overseer/.dotnet",
        // System-wide installations
        "/usr/share/dotnet",
        "/opt/.dotnet",
      };

      return possiblePaths.FirstOrDefault(path => Directory.Exists(path) && File.Exists(Path.Combine(path, "dotnet"))) ?? possiblePaths[0];
    }

    private static string NormalizeVersion(string? version)
    {
      if (string.IsNullOrEmpty(version))
      {
        return "0.0.0";
      }

      // Remove 'v' prefix if present
      if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
      {
        version = version[1..];
      }

      return version;
    }

    private static bool TryCompareVersions(string version1, string version2, out int result)
    {
      result = 0;

      // Try to parse as semantic version
      var v1Parts = version1.Split(['-'], 2);
      var v2Parts = version2.Split(['-'], 2);

      if (!Version.TryParse(v1Parts[0], out var v1) || !Version.TryParse(v2Parts[0], out var v2))
      {
        return false;
      }

      result = v1.CompareTo(v2);

      // If base versions are equal, check pre-release tags
      if (result == 0)
      {
        var hasPreRelease1 = v1Parts.Length > 1;
        var hasPreRelease2 = v2Parts.Length > 1;

        if (hasPreRelease1 && !hasPreRelease2)
        {
          // Pre-release is less than release
          result = -1;
        }
        else if (!hasPreRelease1 && hasPreRelease2)
        {
          // Release is greater than pre-release
          result = 1;
        }
        else if (hasPreRelease1 && hasPreRelease2)
        {
          // Compare pre-release strings
          result = string.Compare(v1Parts[1], v2Parts[1], StringComparison.OrdinalIgnoreCase);
        }
      }

      return true;
    }

    private void InvokeOverseerScript(string action, params string[] args)
    {
      if (!CanAutoUpdate())
      {
        throw new Exception("Restart not supported on this platform.");
      }

      if (!ServiceExists())
      {
        throw new Exception("Overseer service not found. This operation requires the application to be running as a systemd service.");
      }

      var overseerScriptPath = FindOverseerScript();
      if (string.IsNullOrEmpty(overseerScriptPath))
      {
        throw new Exception("Overseer script not found. Please ensure overseer.sh is available.");
      }

      var arguments = args.Length > 0 ? $" {string.Join(" ", args)}" : string.Empty;
      var command = $"nohup /bin/bash {overseerScriptPath} {action}{arguments} >> /var/log/overseer.log 2>&1 &";

      Log.Info($"Executing {action} with command: {command}");

      var processInfo = new ProcessStartInfo
      {
        FileName = "/bin/bash",
        Arguments = $"-c \"{command}\"",
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
      };

      var process = Process.Start(processInfo);
      if (process == null)
      {
        throw new Exception($"Failed to start process for action: {action}");
      }

      process.WaitForExit();
    }
  }
}
