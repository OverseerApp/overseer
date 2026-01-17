namespace Overseer.Server.Automation;

public interface IFailureDetectionModel
{
  float[] GetEmbedding(float[] normalizedImageData);
}
