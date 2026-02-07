using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Users
{
  public class AuthenticationManager(IDataContext context, IHttpContextAccessor httpContextAccessor) : IAuthenticationManager
  {
    readonly IRepository<User> _users = context.Repository<User>();
    readonly HttpContext httpContext = httpContextAccessor.HttpContext!;

    public async Task<UserDisplay?> AuthenticateUser(UserDisplay user)
    {
      return await AuthenticateUser(user.Username, user.Password);
    }

    public async Task<UserDisplay?> AuthenticateUser(string? username, string? password)
    {
      if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
      {
        throw new OverseerException("invalid_username_or_password");
      }

      var user = _users.Get(u => u.Username!.ToLower() == username.ToLower());
      if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
      {
        throw new OverseerException("invalid_username_or_password");
      }

      if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
      {
        throw new OverseerException("invalid_username_or_password");
      }

      return await AuthenticateUser(user);
    }

    public bool AuthenticateToken(string token)
    {
      if (string.IsNullOrWhiteSpace(token))
      {
        return false;
      }

      var tokenHash = HashToken(token);

      // Compare hashed tokens to prevent timing attacks
      var user = _users.Get(u => u.TokenHash == tokenHash);

      //has a matching token that isn't expired.
      return user != null;
    }

    public UserDisplay? DeauthenticateUser(int userId)
    {
      return DeauthenticateUser(_users.GetById(userId));
    }

    public string GetPreauthenticatedToken(int userId)
    {
      var user = _users.GetById(userId);
      if (user == null)
        return string.Empty;

      var token = CreateToken();
      user.PreauthenticatedToken = HashToken(token);
      user.PreauthenticatedTokenExpiration = DateTime.UtcNow.AddMinutes(2);
      _users.Update(user);

      return token;
    }

    public async Task<UserDisplay?> ValidatePreauthenticatedToken(string token)
    {
      var hashedToken = HashToken(token);
      var user = _users.Get(u => u.PreauthenticatedToken == hashedToken && u.PreauthenticatedTokenExpiration > DateTime.UtcNow);
      if (user == null)
        return null;

      return await AuthenticateUser(user);
    }

    private UserDisplay? DeauthenticateUser(User user)
    {
      if (user == null)
      {
        return null;
      }

      user.TokenHash = null;
      _users.Update(user);

      return user.ToDisplay();
    }

    private async Task<UserDisplay> AuthenticateUser(User user)
    {
      // Always generate a new token on login since we only store the hash
      // and cannot return the previous plain token
      var plainToken = CreateToken();

      // Store only the hash of the token, never the plain token
      user.TokenHash = HashToken(plainToken);
      user.PreauthenticatedToken = null;
      user.PreauthenticatedTokenExpiration = null;
      user.LastLogin = DateTime.UtcNow;

      _users.Update(user);

      var claims = new List<Claim>
      {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username!),
        new(ClaimTypes.Role, user.AccessLevel.ToString()),
        new("SessionToken", plainToken!),
      };

      var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
      var authProperties = new AuthenticationProperties
      {
        IsPersistent = true,
        ExpiresUtc = user.SessionLifetime.HasValue ? DateTime.UtcNow.AddDays(user.SessionLifetime.Value) : null,
      };

      await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

      return user.ToDisplay();
    }

    private static string CreateToken()
    {
      var tokenBytes = RandomNumberGenerator.GetBytes(32);
      return Convert.ToBase64String(tokenBytes);
    }

    private static string HashToken(string token)
    {
      var tokenBytes = Encoding.UTF8.GetBytes(token);
      var hashBytes = SHA256.HashData(tokenBytes);
      return Convert.ToBase64String(hashBytes);
    }
  }
}
