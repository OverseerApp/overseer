using System.Security.Claims;
using Overseer.Server.Models;
using Overseer.Server.Users;

namespace Overseer.Server.Api
{
  public static class UsersApi
  {
    public static RouteGroupBuilder MapUsersApi(this RouteGroupBuilder builder)
    {
      var group = builder.MapGroup("/users").WithTags("Users");
      group.RequireAuthorization();

      group.MapGet("/", (IUserManager users) => Results.Ok(users.GetUsers())).RequireAuthorization(AccessLevel.Administrator.ToString());

      group.MapGet("/{id}", (int id, IUserManager users) => Results.Ok(users.GetUser(id))).RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapPost(
          "/",
          (UserDisplay user, IUserManager users, ClaimsPrincipal principal) =>
          {
            var admin = principal.GetUser(users);
            if (admin?.AccessLevel != AccessLevel.Administrator)
            {
              return Results.Forbid();
            }

            return Results.Ok(users.CreateUser(user, admin));
          }
        )
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapPut("/", (UserDisplay user, IUserManager users) => Results.Ok(users.UpdateUser(user)))
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapDelete(
          "/{id}",
          (int id, IUserManager users) =>
          {
            users.DeleteUser(id);
            return Results.Ok();
          }
        )
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapPost(
          "/password",
          (UserDisplay user, IUserManager users, ClaimsPrincipal principal) =>
          {
            return Results.Ok(users.ChangePassword(user, principal.GetUser(users)));
          }
        )
        .RequireAuthorization(AccessLevel.User.ToString());

      return builder;
    }
  }
}
