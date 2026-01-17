using System.Text.Json.Serialization;
using Overseer.Server.Data;

namespace Overseer.Server.Models
{
  public enum AccessLevel
  {
    Readonly = 0,

    //TODO: Add support for a user access level
    //User = 1,

    Administrator = 2,
  }

  /// <summary>
  /// This is the user object that is send to and from the client
  /// </summary>
  public class UserDisplay
  {
    public int Id { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public int? SessionLifetime { get; set; }

    public string? Token { get; set; }

    public bool IsLoggedIn { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccessLevel AccessLevel { get; set; }
  }

  /// <summary>
  /// This is the user as represented in the database
  /// </summary>
  public class User : IEntity
  {
    public int Id { get; set; }

    public string? Username { get; set; }

    public string? PasswordHash { get; set; }

    public string? PasswordSalt { get; set; }

    public string? Token { get; set; }

    /// <summary>
    /// SHA256 hash of the token for secure storage and constant-time comparison
    /// </summary>
    public string? TokenHash { get; set; }

    public DateTime? TokenExpiration { get; set; }

    public int? SessionLifetime { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccessLevel AccessLevel { get; set; }

    public string? PreauthenticatedToken { get; set; }

    public DateTime? PreauthenticatedTokenExpiration { get; set; }

    /// <summary>
    /// Helper method to quickly convert a user to a user display object
    /// </summary>
    public UserDisplay ToDisplay(bool includeToken = false)
    {
      return new UserDisplay
      {
        Id = Id,
        Username = Username,
        SessionLifetime = SessionLifetime,
        Token = includeToken ? Token : null,
        IsLoggedIn = !this.IsTokenExpired(),
        AccessLevel = AccessLevel,
      };
    }
  }

  public static class UserExtensions
  {
    public static bool IsTokenExpired(this User user)
    {
      //if the user or token hash is null it's considered expired
      if (string.IsNullOrWhiteSpace(user?.TokenHash))
        return true;

      //if there is no expiration set the, with the presence of a token, the user
      //is configured for indefinite session length
      if (!user.TokenExpiration.HasValue)
        return false;

      //otherwise the tokens is expired if it's expiration date is less than the current UTC time
      return user.TokenExpiration.Value < DateTime.UtcNow;
    }
  }
}
