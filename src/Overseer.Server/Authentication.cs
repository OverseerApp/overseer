using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Overseer.Server.Users;

namespace Overseer.Server
{
  public static class AuthenticationExtensions
  {
    public static AuthenticationBuilder UseOverseerAuthentication(this AuthenticationBuilder builder, bool isDevelopment)
    {
      return builder.AddCookie(
        CookieAuthenticationDefaults.AuthenticationScheme,
        options =>
        {
          options.Cookie.Name = "Overseer.Session";
          options.Cookie.HttpOnly = true;

          // Development: Cross-port (localhost:4200 -> localhost:9000)
          // Requires SameSite=None + Secure=Always to allow credentials across ports on localhost.
          // Note: Secure cookies work on localhost HTTP.
          if (isDevelopment)
          {
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
          }
          else
          {
            // Production: Same-origin setup.
            // Lax allows proper functionality on local network IPs (insecure HTTP).
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
          }

          options.SlidingExpiration = true;
          options.ExpireTimeSpan = TimeSpan.FromDays(7);
          options.Events.OnRedirectToLogin = context =>
          {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
          };

          options.Events.OnRedirectToAccessDenied = context =>
          {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
          };

          options.Events.OnValidatePrincipal = async context =>
          {
            var authManager = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationManager>();
            var token = context.Principal?.FindFirst("SessionToken")?.Value;

            if (string.IsNullOrEmpty(token) || !authManager.AuthenticateToken(token))
            {
              context.RejectPrincipal();
              await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
          };
        }
      );
    }
  }
}
