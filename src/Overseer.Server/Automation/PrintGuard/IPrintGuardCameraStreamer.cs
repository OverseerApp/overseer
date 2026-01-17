namespace Overseer.Server.Automation.PrintGuard;

public interface IPrintGuardCameraStreamer
{
  void Start(string url);
  void Stop();
  float[] GetProcessedFrame();
}
