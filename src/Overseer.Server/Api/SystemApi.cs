using Overseer.Server.Models;
using Overseer.Server.Services;
using Overseer.Server.System;

namespace Overseer.Server.Api
{
  public static class SystemApi
  {
    public static RouteGroupBuilder MapSystemApi(this RouteGroupBuilder builder)
    {
      var group = builder.MapGroup("/system");
      group.RequireAuthorization();

      group.MapGet("/ping", (IRateLimitingService rateLimiter, HttpContext context) => Results.Ok(new { message = "pong" }));

      group.MapGet(
        "/updates/check",
        async (ISystemManager systemService) =>
        {
          var updateInfo = await systemService.CheckForUpdates();
          return Results.Ok(updateInfo);
        }
      );

      group
        .MapPost(
          "/updates/install",
          (ISystemManager systemService, UpdateInstallRequest request) =>
          {
            if (string.IsNullOrEmpty(request.Version))
            {
              return Results.BadRequest(new { message = "Version is required" });
            }

            systemService.InitiateUpdate(request.Version);
            return Results.Ok();
          }
        )
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group.MapPost(
        "/restart",
        (ISystemManager systemService) =>
        {
          systemService.InitiateRestart();
          return Results.Ok();
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
