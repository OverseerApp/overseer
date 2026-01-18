using Overseer.Server.Models;
using Overseer.Server.Updates;

namespace Overseer.Server.Api
{
  public static class UpdateApi
  {
    public static RouteGroupBuilder MapUpdateApi(this RouteGroupBuilder builder)
    {
      var group = builder.MapGroup("/updates");
      group.RequireAuthorization();

      group.MapGet(
        "/check",
        async (IUpdateService updateService, bool? includePreRelease) =>
        {
          var updateInfo = await updateService.CheckForUpdatesAsync(includePreRelease ?? false);
          return Results.Ok(updateInfo);
        }
      );

      group
        .MapPost(
          "/install",
          async (IUpdateService updateService, UpdateInstallRequest request) =>
          {
            if (string.IsNullOrEmpty(request.Version))
            {
              return Results.BadRequest(new { message = "Version is required" });
            }

            var result = await updateService.InitiateUpdateAsync(request.Version);

            if (result.Success)
            {
              return Results.Ok(result);
            }

            return Results.BadRequest(result);
          }
        )
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group.MapGet(
        "/can-auto-update",
        (IUpdateService updateService) =>
        {
          return Results.Ok(new { canAutoUpdate = updateService.CanAutoUpdate() });
        }
      );

      return builder;
    }
  }

  public class UpdateInstallRequest
  {
    public string? Version { get; set; }
  }
}
