namespace Overseer.Server.Models
{
  /// <summary>
  /// Represents information about an available update from GitHub releases
  /// </summary>
  public class UpdateInfo
  {
    /// <summary>
    /// The current version of the application
    /// </summary>
    public string? CurrentVersion { get; set; }

    /// <summary>
    /// The latest available version from GitHub releases
    /// </summary>
    public string? LatestVersion { get; set; }

    /// <summary>
    /// Indicates if an update is available
    /// </summary>
    public bool UpdateAvailable { get; set; }

    /// <summary>
    /// Indicates if the current platform supports auto-update
    /// </summary>
    public bool CanAutoUpdate { get; set; }

    /// <summary>
    /// The URL to the release page on GitHub
    /// </summary>
    public string? ReleaseUrl { get; set; }

    /// <summary>
    /// The URL to download the release asset
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Release notes/body from the GitHub release
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// The date the release was published
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Indicates if the release is a pre-release
    /// </summary>
    public bool IsPreRelease { get; set; }
  }

  /// <summary>
  /// Represents a GitHub release response
  /// </summary>
  public class GitHubRelease
  {
    public string? TagName { get; set; }
    public string? Name { get; set; }
    public string? Body { get; set; }
    public string? HtmlUrl { get; set; }
    public bool Prerelease { get; set; }
    public bool Draft { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<GitHubReleaseAsset>? Assets { get; set; }
  }

  /// <summary>
  /// Represents an asset attached to a GitHub release
  /// </summary>
  public class GitHubReleaseAsset
  {
    public string? Name { get; set; }
    public string? BrowserDownloadUrl { get; set; }
    public string? ContentType { get; set; }
    public long Size { get; set; }
  }

  /// <summary>
  /// Represents the result of an update operation
  /// </summary>
  public class UpdateResult
  {
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Version { get; set; }
  }
}
