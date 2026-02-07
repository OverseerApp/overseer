using Overseer.Server.Machines;
using Overseer.Server.Models;

namespace Overseer.Server.Api
{
  public static class ControlApi
  {
    public static RouteGroupBuilder MapControlApi(this RouteGroupBuilder builder)
    {
      var group = builder.MapGroup("/control").WithTags("Control");
      group.RequireAuthorization(AccessLevel.User.ToString());

      group.MapPost(
        "/{id}/pause",
        async (int id, IControlManager control) =>
        {
          await control.Pause(id);
          return Results.Ok();
        }
      );

      group.MapPost(
        "/{id}/resume",
        async (int id, IControlManager control) =>
        {
          await control.Resume(id);
          return Results.Ok();
        }
      );

      group.MapPost(
        "/{id}/cancel",
        async (int id, IControlManager control) =>
        {
          await control.Cancel(id);
          return Results.Ok();
        }
      );

      return builder;
    }
  }
}
