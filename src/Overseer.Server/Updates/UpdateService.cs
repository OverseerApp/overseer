using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using log4net;
using Overseer.Server.Models;

namespace Overseer.Server.Updates
{
  public class UpdateService : IUpdateService
  {
    static readonly ILog Log = LogManager.GetLogger(typeof(UpdateService));

    const string GitHubReleasesApiUrl = "https://api.github.com/repos/michaelfdeberry/overseer/releases";
    const string GitHubReleasesUrl = "https://github.com/michaelfdeberry/overseer/releases";
    const string UpdaterScriptName = "overseer.sh";

    readonly IHttpClientFactory _httpClientFactory;
    readonly IWebHostEnvironment _environment;
    readonly JsonSerializerOptions _jsonOptions;

    public UpdateService(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
    {
      _httpClientFactory = httpClientFactory;
      _environment = environment;
      _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(bool includePreRelease = false)
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
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Overseer-UpdateChecker");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

        var response = await client.GetAsync(GitHubReleasesApiUrl);

        if (!response.IsSuccessStatusCode)
        {
          Log.Warn($"Failed to check for updates. Status: {response.StatusCode}");
          return updateInfo;
        }

        var content = await response.Content.ReadAsStringAsync();
        var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(content, _jsonOptions);

        if (releases == null || releases.Count == 0)
        {
          Log.Info("No releases found");
          return updateInfo;
        }

        // Find the latest applicable release
        var latestRelease = releases
          .Where(r => !r.Draft)
          .Where(r => includePreRelease || !r.Prerelease)
          .OrderByDescending(r => r.PublishedAt)
          .FirstOrDefault();

        if (latestRelease == null)
        {
          Log.Info("No applicable releases found");
          return updateInfo;
        }

        var latestVersion = NormalizeVersion(latestRelease.TagName);
        var currentVersionNormalized = NormalizeVersion(currentVersion);

        updateInfo.LatestVersion = latestRelease.TagName;
        updateInfo.ReleaseUrl = latestRelease.HtmlUrl ?? $"{GitHubReleasesUrl}/tag/{latestRelease.TagName}";
        updateInfo.ReleaseNotes = latestRelease.Body;
        updateInfo.PublishedAt = latestRelease.PublishedAt;
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

    public async Task<UpdateResult> InitiateUpdateAsync(string version)
    {
      if (!CanAutoUpdate())
      {
        return new UpdateResult
        {
          Success = false,
          Message = "Auto-update is not supported on this platform. Please update manually.",
          Version = version,
        };
      }

      try
      {
        // Check if the service exists
        if (!ServiceExists())
        {
          return new UpdateResult
          {
            Success = false,
            Message = "Overseer service not found. Auto-update requires the application to be running as a systemd service.",
            Version = version,
          };
        }

        // Find the updater script
        var updaterPath = FindUpdaterScript();
        if (string.IsNullOrEmpty(updaterPath))
        {
          return new UpdateResult
          {
            Success = false,
            Message = "Updater script not found. Please ensure overseer.sh is available.",
            Version = version,
          };
        }

        // Get paths for the updater
        var overseerDirectory = _environment.ContentRootPath;
        var dotnetPath = GetDotNetPath();

        // Launch the updater script in the background
        // The updater will stop this service, perform the update, and restart
        var processInfo = new ProcessStartInfo
        {
          FileName = "/bin/bash",
          Arguments = $"{updaterPath} update {version} \"{overseerDirectory}\" \"{dotnetPath}\"",
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardOutput = false,
          RedirectStandardError = false,
        };

        Log.Info($"Launching updater: {processInfo.FileName} {processInfo.Arguments}");

        // Use nohup to ensure the script continues after this process exits
        processInfo.FileName = "/usr/bin/nohup";
        processInfo.Arguments = $"/bin/bash {updaterPath} update {version} \"{overseerDirectory}\" \"{dotnetPath}\"";

        Process.Start(processInfo);

        return new UpdateResult
        {
          Success = true,
          Message = "Update initiated. The application will restart automatically.",
          Version = version,
        };
      }
      catch (Exception ex)
      {
        Log.Error("Error initiating update", ex);
        return new UpdateResult
        {
          Success = false,
          Message = $"Failed to initiate update: {ex.Message}",
          Version = version,
        };
      }
    }

    public bool CanAutoUpdate()
    {
      // Auto-update is only supported on Linux with systemd
      return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && ServiceExists();
    }

    private bool ServiceExists()
    {
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        return false;
      }

      var servicePath = "/lib/systemd/system/overseer.service";
      return File.Exists(servicePath);
    }

    private string? FindUpdaterScript()
    {
      // Look for the updater script in several locations
      var searchPaths = new[]
      {
        Path.Combine(_environment.ContentRootPath, "scripts", UpdaterScriptName),
        Path.Combine(_environment.ContentRootPath, "..", "scripts", UpdaterScriptName),
        Path.Combine("/opt/overseer/scripts", UpdaterScriptName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "overseer/scripts", UpdaterScriptName),
      };

      foreach (var path in searchPaths)
      {
        if (File.Exists(path))
        {
          return path;
        }
      }

      return null;
    }

    private string GetDotNetPath()
    {
      // Try to determine the .NET path
      var possiblePaths = new[]
      {
        Path.Combine(Directory.GetCurrentDirectory(), ".dotnet"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
        "/usr/share/dotnet",
        "/opt/.dotnet",
      };

      foreach (var path in possiblePaths)
      {
        if (Directory.Exists(path) && File.Exists(Path.Combine(path, "dotnet")))
        {
          return path;
        }
      }

      return possiblePaths[0]; // Default to relative path
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
  }
}
