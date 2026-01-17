using Overseer.Server.Automation.PrintGuard;
using Overseer.Server.Channels;
using Overseer.Server.Machines;
using Overseer.Server.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using IConfigurationManager = Overseer.Server.Settings.IConfigurationManager;

namespace Overseer.Tests.Machines;

public class FakeCameraStreamer(string resourceName) : IPrintGuardCameraStreamer
{
    public float[] GetProcessedFrame()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return [];

        // Load directly with ImageSharp to avoid Emgu.CV native dependency
        using var image = Image.Load<Rgb24>(stream);

        // Preprocess inline (same logic as CameraStreamer.PreprocessImage)
        float[] mean = [0.485f, 0.456f, 0.406f];
        float[] std = [0.229f, 0.224f, 0.225f];
        int targetSize = 256; // Resize to 256 first
        int cropSize = 224; // Then center crop to 224

        // Convert to grayscale (matching PrintGuard's preprocessing)
        image.Mutate(x => x.Grayscale());

        // Resize to 256
        image.Mutate(x =>
            x.Resize(
                new ResizeOptions
                {
                    Size = new Size(targetSize, targetSize),
                    Mode = ResizeMode.Crop,
                }
            )
        );

        // Center crop to 224x224
        int cropX = (targetSize - cropSize) / 2;
        int cropY = (targetSize - cropSize) / 2;
        image.Mutate(x => x.Crop(new Rectangle(cropX, cropY, cropSize, cropSize)));

        float[] normalizedData = new float[3 * cropSize * cropSize];

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                // Grayscale image - all RGB channels have the same value
                // Normalize the grayscale value and replicate across all 3 channels
                float grayValue = pixel.R / 255.0f;

                normalizedData[0 * cropSize * cropSize + y * cropSize + x] =
                    (grayValue - mean[0]) / std[0];
                normalizedData[1 * cropSize * cropSize + y * cropSize + x] =
                    (grayValue - mean[1]) / std[1];
                normalizedData[2 * cropSize * cropSize + y * cropSize + x] =
                    (grayValue - mean[2]) / std[2];
            }
        }

        return normalizedData;
    }

    public void Start(string url)
    {
        return;
    }

    public void Stop()
    {
        return;
    }
}

public class JobSentinelTests : IDisposable
{
    private readonly Mock<IConfigurationManager> _mockConfigurationManager;
    private readonly JobFailureChannel _jobFailureChannel;
    private readonly OctoprintMachine _testMachine;
    private readonly MachineJob _testJob;
    private readonly ApplicationSettings _defaultSettings;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public JobSentinelTests()
    {
        var httpClient = new HttpClient();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _mockConfigurationManager = new Mock<IConfigurationManager>();
        _jobFailureChannel = new JobFailureChannel();

        _testMachine = new OctoprintMachine
        {
            Id = 1,
            Name = "Test Printer",
            WebCamUrl = "http://localhost:8080/webcam",
        };

        _testJob = new MachineJob
        {
            Id = 100,
            MachineId = 1,
            State = MachineState.Operational,
        };

        _defaultSettings = new ApplicationSettings
        {
            EnableAiMonitoring = true,
            AiMonitoringFrameCaptureRate = 5, // Fast rate for testing (1000 fps = 1ms interval)
            AiMonitoringFailureAction = AIMonitoringFailureAction.CancelJob,
        };

        _mockConfigurationManager.Setup(x => x.GetApplicationSettings()).Returns(_defaultSettings);
    }

    public void Dispose()
    {
        _jobFailureChannel.Dispose();
        GC.SuppressFinalize(this);
    }

    private JobSentinel CreateSentinel(string imageResource)
    {
        var failureDetectionAnalyzer = new PrintGuardFailureDetectionAnalyzer(
            new PrintGuardModel(_mockHttpClientFactory.Object),
            new FakeCameraStreamer(imageResource)
        );

        return new JobSentinel(
            _testMachine,
            _testJob,
            failureDetectionAnalyzer,
            _mockConfigurationManager.Object,
            _jobFailureChannel
        );
    }

    [Fact]
    public async Task ShouldDetectFailure()
    {
        var sentinel = CreateSentinel("Overseer.Tests.Resources.fail.jpg");
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        sentinel.StartMonitoring(cts.Token);
        // Wait sufficient time for multiple frames to be processed
        var failureMessage = await _jobFailureChannel.ReadAsync(Guid.NewGuid(), cts.Token);
        Assert.NotNull(failureMessage);
        Assert.Equal(_testJob.Id, failureMessage.JobId);
        Assert.True(failureMessage.IsFailureDetected);
    }

    [Fact]
    public async Task ShouldDetectSuccess()
    {
        var sentinel = CreateSentinel("Overseer.Tests.Resources.pass.jpg");
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        sentinel.StartMonitoring(cts.Token);

        try
        {
            var failureMessage = await _jobFailureChannel.ReadAsync(Guid.NewGuid(), cts.Token);
            Assert.Null(failureMessage);
        }
        catch (OperationCanceledException)
        {
            // Test passes - no failure was detected before timeout
        }
    }
}
