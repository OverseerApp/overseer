using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Overseer.Server.Automation;

public class CameraStreamer : ICameraStreamer
{
  private VideoCapture? _capture;
  private Mat _latestFrame = new();
  private readonly object _frameLock = new();
  private bool _disposed;

  public void Start(string url)
  {
    // For RTSP/MJPEG, use the URL provided by your printer (OctoPrint/Mainsail)
    _capture = new VideoCapture(url);

    // Optimization: Use MJPG if supported to reduce bandwidth
    _capture.Set(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));

    // Hook into the ImageGrabbed event (Runs on a background thread)
    _capture.ImageGrabbed += (s, e) =>
    {
      lock (_frameLock)
      {
        // Grabs the frame into the Mat without blocking the main thread
        _capture?.Retrieve(_latestFrame);
      }
    };

    _capture.Start();
  }

  public void Stop()
  {
    if (_capture != null)
    {
      _capture.Stop();
      _capture.Dispose();
      _capture = null;
    }
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (_disposed)
      return;

    if (disposing)
    {
      Stop();
      lock (_frameLock)
      {
        _latestFrame.Dispose();
      }
    }

    _disposed = true;
  }

  public float[] GetProcessedFrame()
  {
    lock (_frameLock)
    {
      using var frame = _latestFrame.Clone();
      return PreprocessImage(frame);
    }
  }

  public static float[] PreprocessImage(Mat frame)
  {
    if (frame.IsEmpty)
      return [];

    // 1. Define ImageNet constants
    float[] mean = [0.485f, 0.456f, 0.406f];
    float[] std = [0.229f, 0.224f, 0.225f];
    int width = 224;
    int height = 224;

    // 2. Convert Mat to ImageSharp Image and Resize
    using var buffer = new VectorOfByte();
    CvInvoke.Imencode(".png", frame, buffer);
    using var ms = new MemoryStream(buffer.ToArray());
    using var image = Image.Load<Rgb24>(ms);
    image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(width, height), Mode = ResizeMode.Max }));

    // 3. Prepare the flat array (CHW format: RRR... GGG... BBB...)
    float[] normalizedData = new float[3 * width * height];

    image.ProcessPixelRows(accessor =>
    {
      for (int y = 0; y < height; y++)
      {
        var row = accessor.GetRowSpan(y);
        for (int x = 0; x < width; x++)
        {
          // Normalize and assign to specific channel offsets
          // Red Channel
          normalizedData[0 * width * height + y * width + x] = ((row[x].R / 255.0f) - mean[0]) / std[0];
          // Green Channel
          normalizedData[1 * width * height + y * width + x] = ((row[x].G / 255.0f) - mean[1]) / std[1];
          // Blue Channel
          normalizedData[2 * width * height + y * width + x] = ((row[x].B / 255.0f) - mean[2]) / std[2];
        }
      }
    });

    return normalizedData;
  }
}
