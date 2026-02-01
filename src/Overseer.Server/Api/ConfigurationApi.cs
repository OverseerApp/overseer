using Overseer.Server.Machines;
using Overseer.Server.Models;
using Overseer.Server.Users;
using IConfigurationManager = Overseer.Server.Settings.IConfigurationManager;

namespace Overseer.Server.Api
{
  public static class ConfigurationApi
  {
    public static RouteGroupBuilder MapConfigurationApi(this RouteGroupBuilder builder)
    {
      var group = builder.MapGroup("/settings").WithTags("Configuration");
      group.RequireAuthorization();

      group.MapGet("/", (IConfigurationManager configuration) => Results.Ok(configuration.GetApplicationSettings()));

      group
        .MapPut(
          "/",
          async (ApplicationSettings settings, IConfigurationManager configuration) =>
          {
            var updatedSettings = await configuration.UpdateApplicationSettings(settings);
            return Results.Ok(updatedSettings);
          }
        )
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapPost(
          "/certificate",
          (CertificateDetails certificate, IConfigurationManager configuration) => Results.Ok(configuration.AddCertificateExclusion(certificate))
        )
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      // using the cold cache policy as this data will only change with new releases
      group.MapGet("/about", (IConfigurationManager configuration) => Results.Ok(configuration.GetApplicationInfo())).CacheOutput("ColdCache");

      return builder;
    }
  }
}
