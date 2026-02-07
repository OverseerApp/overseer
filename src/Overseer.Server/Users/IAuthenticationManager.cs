using Overseer.Server.Models;

namespace Overseer.Server.Users;

public interface IAuthenticationManager
{
  bool AuthenticateToken(string token);
  Task<UserDisplay?> AuthenticateUser(string? username, string? password);
  Task<UserDisplay?> AuthenticateUser(UserDisplay user);
  UserDisplay? DeauthenticateUser(int userId);
  string? GetPreauthenticatedToken(int userId);
  Task<UserDisplay?> ValidatePreauthenticatedToken(string token);
}
