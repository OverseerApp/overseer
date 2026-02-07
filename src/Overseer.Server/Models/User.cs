using System.Text.Json.Serialization;
using Overseer.Server.Data;

namespace Overseer.Server.Models
{
  public enum AccessLevel
  {
    Readonly = 0,

    User = 1,

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

    public DateTime? LastLogin { get; set; }

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

    /// <summary>
    /// SHA256 hash of the token for secure storage and constant-time comparison
    /// </summary>
    public string? TokenHash { get; set; }

    public DateTime? LastLogin { get; set; }
    public int? SessionLifetime { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AccessLevel AccessLevel { get; set; }

    public string? PreauthenticatedToken { get; set; }

    public DateTime? PreauthenticatedTokenExpiration { get; set; }

    /// <summary>
    /// Helper method to quickly convert a user to a user display object
    /// </summary>
    public UserDisplay ToDisplay()
    {
      return new UserDisplay
      {
        Id = Id,
        Username = Username,
        SessionLifetime = SessionLifetime,
        AccessLevel = AccessLevel,
        LastLogin = LastLogin,
      };
    }
  }
}
