using System.Security.Cryptography;
using System.Text;
using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Users
{
  public class AuthenticationManager(IDataContext context) : IAuthenticationManager
  {
    const string Bearer = "Bearer";

    readonly IRepository<User> _users = context.Repository<User>();

    /// <summary>
    /// Creates a SHA256 hash of the token for secure storage comparison.
    /// This prevents timing attacks by allowing constant-time comparison of hashes.
    /// </summary>
    static string HashToken(string token)
    {
      var tokenBytes = Encoding.UTF8.GetBytes(token);
      var hashBytes = SHA256.HashData(tokenBytes);
      return Convert.ToBase64String(hashBytes);
    }

    public UserDisplay? AuthenticateUser(UserDisplay user)
    {
      return AuthenticateUser(user.Username, user.Password);
    }

    public UserDisplay? AuthenticateUser(string? username, string? password)
    {
      if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
      {
        throw new OverseerException("invalid_username_or_password");
      }

      var user = _users.Get(u => u.Username!.ToLower() == username.ToLower()) ?? throw new OverseerException("invalid_username");
      if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
      {
        throw new OverseerException("invalid_password");
      }

      return AuthenticateUser(user);
    }

    public User? AuthenticateToken(string token)
    {
      if (string.IsNullOrWhiteSpace(token))
      {
        return null;
      }

      var strippedToken = StripToken(token);
      var tokenHash = HashToken(strippedToken);

      // Compare hashed tokens to prevent timing attacks
      var user = _users.Get(u => u.TokenHash == tokenHash);

      if (user.IsTokenExpired())
      {
        return null;
      }

      //has a matching token that isn't expired.
      return user;
    }

    public UserDisplay? DeauthenticateUser(string token)
    {
      if (string.IsNullOrWhiteSpace(token))
      {
        return null;
      }

      var tokenHash = HashToken(StripToken(token));
      return DeauthenticateUser(_users.Get(u => u.TokenHash == tokenHash));
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
      if (user.AccessLevel != AccessLevel.Readonly)
        return string.Empty;

      var token = Encoding.UTF8.GetBytes(BCrypt.Net.BCrypt.GenerateSalt(16));
      user.PreauthenticatedToken = Convert.ToBase64String(token);
      user.PreauthenticatedTokenExpiration = DateTime.UtcNow.AddMinutes(2);
      _users.Update(user);

      return user.PreauthenticatedToken;
    }

    public UserDisplay? ValidatePreauthenticatedToken(string token)
    {
      var user = _users.Get(u => u.PreauthenticatedToken == token && u.PreauthenticatedTokenExpiration > DateTime.UtcNow);
      if (user == null)
        return null;

      return AuthenticateUser(user);
    }

    static string StripToken(string token)
    {
      if (string.IsNullOrWhiteSpace(token))
      {
        return string.Empty;
      }

      return token.Replace(Bearer, string.Empty).Trim();
    }

    UserDisplay? DeauthenticateUser(User user)
    {
      if (user == null)
      {
        return null;
      }

      user.Token = null;
      user.TokenHash = null;
      user.TokenExpiration = null;
      _users.Update(user);

      return user.ToDisplay();
    }

    UserDisplay AuthenticateUser(User user)
    {
      if (!user.IsTokenExpired())
      {
        return user.ToDisplay(includeToken: true);
      }

      var tokenBytes = RandomNumberGenerator.GetBytes(32);
      var plainToken = Convert.ToBase64String(tokenBytes);

      // Store the hash of the token, not the plain token
      user.Token = plainToken; // Kept temporarily for ToDisplay, cleared after
      user.TokenHash = HashToken(plainToken);

      if (user.SessionLifetime.HasValue)
      {
        user.TokenExpiration = DateTime.UtcNow.AddDays(user.SessionLifetime.Value);
      }
      else
      {
        user.TokenExpiration = null;
      }

      user.PreauthenticatedToken = null;
      user.PreauthenticatedTokenExpiration = null;

      _users.Update(user);

      // Return display with token, then clear plain token from storage
      var display = user.ToDisplay(includeToken: true);
      user.Token = null;
      _users.Update(user);

      return display;
    }
  }
}
