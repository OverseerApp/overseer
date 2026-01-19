using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Overseer.Server.Models
{
  public class ApplicationInfo
  {
    static readonly Lazy<ApplicationInfo> AppInfoLazy = new Lazy<ApplicationInfo>(() => new ApplicationInfo());

    public static ApplicationInfo Instance => AppInfoLazy.Value;

    public ApplicationInfo()
    {
      var assembly = Assembly.GetAssembly(GetType());
      if (assembly != null)
      {
        var version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        var plusIndex = version?.LastIndexOf('+') ?? -1;
        Version = plusIndex > 0 ? version?[..plusIndex] : version;
      }
      Platform = Environment.OSVersion.Platform.ToString();
      OperatingSystem = Environment.OSVersion.VersionString;
      MachineName = Environment.MachineName;
      Runtime = RuntimeInformation.FrameworkDescription;
    }

    public string? Platform { get; }

    public string? OperatingSystem { get; }

    public string? MachineName { get; }

    public string? Version { get; }

    public string? Runtime { get; set; }
  }
}
