using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Overseer.Server.Automation.PrintGuard;

public class PrintGuardCameraStreamer : IPrintGuardCameraStreamer, IDisposable
{
  private VideoCapture? _capture;
  private Mat _latestFrame = new();
  private readonly Lock _frameLock = new();
  private bool _disposed;

  public void Start(string url)
  {
    _capture = new VideoCapture(url);
    _capture.Set(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
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
    int targetSize = 256; // Resize to 256 first
    int cropSize = 224; // Then center crop to 224

    // 2. Convert Mat to ImageSharp Image, convert to Grayscale, and Resize
    using var buffer = new VectorOfByte();
    CvInvoke.Imencode(".png", frame, buffer);
    using var ms = new MemoryStream(buffer.ToArray());
    using var image = Image.Load<Rgb24>(ms);

    // Convert to grayscale (matching PrintGuard's preprocessing)
    image.Mutate(x => x.Grayscale());

    // Resize to 256
    image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(targetSize, targetSize), Mode = ResizeMode.Crop }));

    // Center crop to 224x224
    int cropX = (targetSize - cropSize) / 2;
    int cropY = (targetSize - cropSize) / 2;
    image.Mutate(x => x.Crop(new Rectangle(cropX, cropY, cropSize, cropSize)));

    // 3. Prepare the flat array (CHW format: RRR... GGG... BBB...)
    // Even though grayscale, we replicate across 3 channels (as PrintGuard does)
    float[] normalizedData = new float[3 * cropSize * cropSize];

    image.ProcessPixelRows(accessor =>
    {
      for (int y = 0; y < cropSize; y++)
      {
        var row = accessor.GetRowSpan(y);
        for (int x = 0; x < cropSize; x++)
        {
          // Grayscale image - all RGB channels have the same value
          // Normalize the grayscale value and replicate across all 3 channels
          float grayValue = row[x].R / 255.0f;

          // Red Channel
          normalizedData[0 * cropSize * cropSize + y * cropSize + x] = (grayValue - mean[0]) / std[0];
          // Green Channel
          normalizedData[1 * cropSize * cropSize + y * cropSize + x] = (grayValue - mean[1]) / std[1];
          // Blue Channel
          normalizedData[2 * cropSize * cropSize + y * cropSize + x] = (grayValue - mean[2]) / std[2];
        }
      }
    });

    return normalizedData;
  }
}
