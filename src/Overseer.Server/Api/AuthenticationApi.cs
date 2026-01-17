using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Overseer.Server.Models;
using Overseer.Server.Services;
using Overseer.Server.Users;

namespace Overseer.Server.Api
{
  public static class AuthenticationApi
  {
    public static RouteGroupBuilder MapAuthenticationApi(this RouteGroupBuilder builder)
    {
      var group = builder.MapGroup("/auth");

      group.MapGet(
        "/",
        (ClaimsPrincipal? currentUser, IAuthorizationManager authorizationManager) =>
        {
          var isAuthenticated = currentUser?.Identity?.IsAuthenticated ?? false;
          if (isAuthenticated)
            return Results.Ok();

          return Results.Text($"requiresInitialization={authorizationManager.RequiresAuthorization()}", statusCode: (int)HttpStatusCode.Unauthorized);
        }
      );

      group.MapPost(
        "/setup",
        (UserDisplay user, IAuthorizationManager authorizationManager, IUserManager userManager) =>
        {
          if (!authorizationManager.RequiresAuthorization())
            return Results.StatusCode((int)HttpStatusCode.PreconditionFailed);
          return Results.Ok(userManager.CreateUser(user));
        }
      );

      group.MapPost(
        "/login",
        (UserDisplay user, HttpContext httpContext, IAuthenticationManager authenticationManager, IRateLimitingService rateLimiter) =>
        {
          // Use IP address as the rate limit key
          var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
          var rateLimitKey = $"login:{clientIp}";

          if (rateLimiter.IsRateLimited(rateLimitKey))
          {
            return Results.Problem(
              title: "Too Many Requests",
              detail: "Too many failed login attempts. Please try again later.",
              statusCode: (int)HttpStatusCode.TooManyRequests
            );
          }

          try
          {
            var result = authenticationManager.AuthenticateUser(user);
            // Reset rate limit on successful login
            rateLimiter.Reset(rateLimitKey);
            return Results.Ok(result);
          }
          catch (OverseerException)
          {
            // Record failed attempt
            rateLimiter.RecordAttempt(rateLimitKey);
            throw;
          }
        }
      );

      group
        .MapDelete(
          "/logout",
          ([FromHeader(Name = "Authorization")] string authorization, IAuthenticationManager authenticationManager) =>
          {
            authenticationManager.DeauthenticateUser(authorization);
            return Results.Ok();
          }
        )
        .RequireAuthorization();

      group
        .MapPost("/logout/{id}", (int id, IAuthenticationManager authenticationManager) => Results.Ok(authenticationManager.DeauthenticateUser(id)))
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group
        .MapGet("/sso/{id}", (int id, IAuthenticationManager authenticationManager) => Results.Ok(authenticationManager.GetPreauthenticatedToken(id)))
        .RequireAuthorization(AccessLevel.Administrator.ToString());

      group.MapPost(
        "/sso",
        (string token, IAuthenticationManager authenticationManager) => Results.Ok(authenticationManager.ValidatePreauthenticatedToken(token))
      );

      return builder;
    }
  }
}
