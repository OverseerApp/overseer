using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Overseer.Server.Automation;

public class PrintGuardFailureDetectionModel : IFailureDetectionModel, IDisposable
{
  private const string ModelUrl = "https://huggingface.co/oliverbravery/PrintGuard/resolve/main/model.onnx";
  private const string ModelFileName = "model.onnx";
  private const string OverseerDirectoryName = "overseer";

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly Lazy<InferenceSession> _lazySession;
  private string? _inputName;

  public PrintGuardFailureDetectionModel(IHttpClientFactory httpClientFactory)
  {
    _httpClientFactory = httpClientFactory;
    _lazySession = new Lazy<InferenceSession>(InitializeSession, LazyThreadSafetyMode.ExecutionAndPublication);
  }

  private InferenceSession InitializeSession()
  {
    var modelPath = GetModelPath();
    EnsureModelDownloaded(modelPath, _httpClientFactory).GetAwaiter().GetResult();

    var options = new Microsoft.ML.OnnxRuntime.SessionOptions();
    // TODO: Configure options as needed (e.g., GPU, optimization level)
    var session = new InferenceSession(modelPath, options);
    _inputName = session.InputMetadata.Keys.First();
    return session;
  }

  private static string GetModelPath()
  {
    var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(userDirectory, OverseerDirectoryName, ModelFileName);
  }

  private static async Task EnsureModelDownloaded(string modelPath, IHttpClientFactory httpClientFactory)
  {
    if (File.Exists(modelPath))
      return;

    var directory = Path.GetDirectoryName(modelPath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      Directory.CreateDirectory(directory);

    using var httpClient = httpClientFactory.CreateClient();
    httpClient.Timeout = TimeSpan.FromMinutes(10); // Large model files may take time

    using var response = await httpClient.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    await using var contentStream = await response.Content.ReadAsStreamAsync();
    await using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
    await contentStream.CopyToAsync(fileStream);
  }

  public float[] GetEmbedding(float[] normalizedImageData)
  {
    // 1. Create the input tensor
    // Shape for ShuffleNet/ImageNet: [BatchSize, Channels, Height, Width]
    var inputTensor = new DenseTensor<float>(normalizedImageData, [1, 3, 224, 224]);

    // 2. Wrap in NamedOnnxValue
    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };

    // 3. Run Inference
    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _lazySession.Value.Run(inputs);

    // 4. Extract the embedding as an array
    // We take the first output as a float array
    return [.. results[0].AsEnumerable<float>()];
  }

  public void Dispose()
  {
    if (_lazySession.IsValueCreated)
      _lazySession.Value.Dispose();
    GC.SuppressFinalize(this);
  }
}
