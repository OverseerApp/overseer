using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
      var group = builder.MapGroup("/auth").WithTags("Authentication");

      group.MapGet(
        "/",
        (ClaimsPrincipal? currentUser, IAuthorizationManager authorizationManager, IUserManager userManager) =>
        {
          var isAuthenticated = currentUser?.Identity?.IsAuthenticated ?? false;
          var userIdClaim = currentUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
          if (isAuthenticated && int.TryParse(userIdClaim, out var userId))
          {
            var user = userManager.GetUser(userId);
            if (user is not null)
            {
              return Results.Ok(user);
            }
          }

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
        async (UserDisplay user, HttpContext httpContext, IAuthenticationManager authenticationManager, IRateLimitingService rateLimiter) =>
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
            var result = await authenticationManager.AuthenticateUser(user);
            if (result == null)
            {
              rateLimiter.RecordAttempt(rateLimitKey);
              return Results.Unauthorized();
            }

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
          async (ClaimsPrincipal? currentUser, HttpContext httpContext, IAuthenticationManager authenticationManager) =>
          {
            var userIdClaim = currentUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
              authenticationManager.DeauthenticateUser(userId);
            }

            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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
        async ([FromQuery] string token, HttpContext httpContext, IAuthenticationManager authenticationManager) =>
        {
          var user = await authenticationManager.ValidatePreauthenticatedToken(token);
          if (user is null)
          {
            return Results.Unauthorized();
          }

          return Results.Ok(user);
        }
      );

      return builder;
    }
  }
}
