namespace Overseer.Server.Plugins.Models;

public record PluginRegistryItem(
  string Name,
  string Author,
  string Description,
  string GithubRepository,
  string License,
  string? Version,
  string? DownloadUrl,
  bool? IsInstalled,
  string? InstalledVersion,
  bool? IsUpdateAvailable
);
