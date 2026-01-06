namespace Overseer.Server.Models
{
  public enum AIMonitoringFailureAction
  {
    AlertOnly = 0,
    PauseJob = 1,
    CancelJob = 2,
  }

  public class ApplicationSettings
  {
    public const int DefaultPort = 9000;
    public const int DefaultInterval = 10000;

    /// <summary>
    /// How often the app will poll the machine for information
    /// </summary>
    public int Interval { get; set; } = DefaultInterval;

    /// <summary>
    /// If true disabled machine won't be visible on the monitoring screen
    /// </summary>
    public bool HideDisabledMachines { get; set; }

    /// <summary>
    /// If true machines that aren't currently aren't operation won't be
    /// visible on the monitoring screen
    /// </summary>
    public bool HideIdleMachines { get; set; }

    /// <summary>
    /// If true the monitoring view will be sorted by the estimated
    /// remaining time.
    /// </summary>
    public bool SortByTimeRemaining { get; set; }

    /// <summary>
    /// Gets/Sets the port which the overseer daemon listens on
    /// </summary>
    public int LocalPort { get; set; } = DefaultPort;

    /// <summary>
    /// If true AI based monitoring is enabled.
    /// </summary>
    public bool EnableAiMonitoring { get; set; } = false;

    /// <summary>
    /// Interval in minutes between AI monitoring scans.
    /// </summary>
    public int AiMonitoringScanInterval { get; set; } = 1;

    /// <summary>
    /// Action to take when AI monitoring detects a failure.
    /// </summary>
    public AIMonitoringFailureAction AiMonitoringFailureAction { get; set; } = AIMonitoringFailureAction.AlertOnly;
  }
}
