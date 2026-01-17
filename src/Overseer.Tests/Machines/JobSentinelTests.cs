using Overseer.Server.Automation;
using Overseer.Server.Channels;
using Overseer.Server.Machines;
using Overseer.Server.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using IConfigurationManager = Overseer.Server.Settings.IConfigurationManager;

namespace Overseer.Tests.Machines;

public class FakeCameraStreamer : ICameraStreamer
{
    public void Dispose()
    {
        return;
    }

    public float[] GetProcessedFrame()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(
            "Overseer.Tests.Resources.postfail.png"
        );
        if (stream == null)
            return [];

        // Load directly with ImageSharp to avoid Emgu.CV native dependency
        using var image = Image.Load<Rgb24>(stream);

        // Preprocess inline (same logic as CameraStreamer.PreprocessImage)
        float[] mean = [0.485f, 0.456f, 0.406f];
        float[] std = [0.229f, 0.224f, 0.225f];
        int width = 224;
        int height = 224;

        image.Mutate(x =>
            x.Resize(new ResizeOptions { Size = new Size(width, height), Mode = ResizeMode.Max })
        );

        float[] normalizedData = new float[3 * width * height];

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                normalizedData[0 * width * height + y * width + x] =
                    ((pixel.R / 255.0f) - mean[0]) / std[0];
                normalizedData[1 * width * height + y * width + x] =
                    ((pixel.G / 255.0f) - mean[1]) / std[1];
                normalizedData[2 * width * height + y * width + x] =
                    ((pixel.B / 255.0f) - mean[2]) / std[2];
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
    // The prototypes in the actual system use 192-dimensional embeddings
    private const int EmbeddingDimension = 192;

    private readonly ICameraStreamer _fakeCameraStreamer;
    private readonly IFailureDetectionModel _failureDetectionModel;
    private readonly Mock<IConfigurationManager> _mockConfigurationManager;
    private readonly JobFailureChannel _jobFailureChannel;
    private readonly OctoprintMachine _testMachine;
    private readonly MachineJob _testJob;
    private readonly ApplicationSettings _defaultSettings;

    public JobSentinelTests()
    {
        var httpClient = new HttpClient();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        _fakeCameraStreamer = new FakeCameraStreamer();
        _failureDetectionModel = new PrintGuardFailureDetectionModel(mockHttpClientFactory.Object);
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

    private JobSentinel CreateSentinel()
    {
        return new JobSentinel(
            _testMachine,
            _testJob,
            _fakeCameraStreamer,
            _failureDetectionModel,
            _mockConfigurationManager.Object,
            _jobFailureChannel
        );
    }

    [Fact]
    public async Task ShouldDetectFailure()
    {
        var sentinel = CreateSentinel();
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        sentinel.StartMonitoring(cts.Token);
        // Wait sufficient time for multiple frames to be processed
        var failureMessage = await _jobFailureChannel.ReadAsync(Guid.NewGuid(), cts.Token);
        Assert.NotNull(failureMessage);
    }
}
