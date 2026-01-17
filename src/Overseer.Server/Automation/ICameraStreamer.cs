namespace Overseer.Server.Automation;

public interface ICameraStreamer : IDisposable
{
  void Start(string url);
  void Stop();
  float[] GetProcessedFrame();
}
