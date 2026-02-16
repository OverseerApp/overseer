using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Overseer.Server.Data;

namespace Overseer.Server.Models
{
  public class CertificateDetails
  {
    public CertificateDetails() { }

    public CertificateDetails(X509Certificate certificate)
    {
      IssuedTo = certificate.Subject.Replace("CN=", string.Empty);
      IssuedBy = certificate.Issuer.Replace("CN=", string.Empty);
      IssueDate = certificate.GetEffectiveDateString();
      ExpireDate = certificate.GetExpirationDateString();
      Thumbprint = certificate.GetCertHashString()?.ToLower();
    }

    public int Id { get; set; }

    public string? IssuedTo { get; set; }

    public string? IssuedBy { get; set; }

    public string? IssueDate { get; set; }

    public string? ExpireDate { get; set; }

    public string? Thumbprint { get; set; }

    public override string ToString()
    {
      return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
  }
}
