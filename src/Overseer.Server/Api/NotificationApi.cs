using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Api;

public static class NotificationApi
{
  public static RouteGroupBuilder MapNotificationApi(this RouteGroupBuilder builder)
  {
    var group = builder.MapGroup("/notifications").WithTags("Notifications");
    group.RequireAuthorization();

    group.MapGet(
      "/",
      (IDataContext dataContext) =>
      {
        var notifications = dataContext.Repository<Notification>().GetAll().OrderByDescending(n => n.Timestamp).ToList();
        return Results.Ok(notifications);
      }
    );

    group.MapPost(
      "/read",
      (IDataContext dataContext, List<int> ids) =>
      {
        var notifications = dataContext.Repository<Notification>().Filter(n => ids.Contains(n.Id) && !n.IsRead).ToList();
        foreach (var notification in notifications)
        {
          notification.IsRead = true;
        }

        dataContext.Repository<Notification>().Update(notifications);
        return Results.Ok();
      }
    );

    group.MapDelete(
      "/",
      (IDataContext dataContext) =>
      {
        dataContext.Repository<Notification>().DeleteAll();
        return Results.Ok();
      }
    );

    group.MapDelete(
      "/{id:int}",
      (IDataContext dataContext, int id) =>
      {
        dataContext.Repository<Notification>().Delete(id);
        return Results.Ok();
      }
    );

    return builder;
  }
}
