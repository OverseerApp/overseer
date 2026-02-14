using System.Net;
using System.Net.Security;
using log4net;
using Overseer.Server.Channels;
using Overseer.Server.Integration.Common;
using Overseer.Server.Models;

namespace Overseer.Server.Services;

/// <summary>
/// Service that listens for certificate exceptions and manages certificate exclusions.
/// </summary>
public class CertificateExceptionService : BackgroundService
{
  static readonly ILog Log = LogManager.GetLogger(typeof(CertificateExceptionService));

  readonly Guid _subscriberId = Guid.NewGuid();
  readonly HashSet<string> _exclusions = [];
  readonly ICertificateExceptionChannel _certificateExceptionChannel;

  public CertificateExceptionService(Settings.IConfigurationManager configurationManager, ICertificateExceptionChannel certificateExceptionChannel)
  {
    _certificateExceptionChannel = certificateExceptionChannel;
    configurationManager.GetExcludedCertificates().ToList().ForEach(c => _exclusions.Add(c.Thumbprint!.ToLower()));

    ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, change, errors) =>
    {
      if (errors == SslPolicyErrors.None)
        return true;
      if (certificate == null)
        return true;
      if (_exclusions.Contains(certificate.GetCertHashString()?.ToLower() ?? string.Empty))
        return true;

      Log.Warn(
        @$"Certificate error: {certificate.Subject}.
        Issued by {certificate.Issuer} 
        Issued on {certificate.GetEffectiveDateString()} 
        Expires on {certificate.GetExpirationDateString()}"
      );
      throw new OverseerException("certificate_exception", new CertificateDetails(certificate));
    };
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var certificateException = await _certificateExceptionChannel.ReadAsync(_subscriberId, stoppingToken);
        if (certificateException?.Thumbprint != null)
        {
          Log.Info($"Certificate exclusion add for {certificateException.Thumbprint}");
          _exclusions.Add(certificateException.Thumbprint.ToLower());
        }
      }
      catch (Exception ex)
      {
        Log.Error("Error processing certificate exception", ex);
      }
    }
  }
}
