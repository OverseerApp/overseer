using System.Security.Claims;
using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Users;

public class AuthorizationManager(IDataContext context, IAuthenticationManager authenticationManager) : IAuthorizationManager
{
  readonly IAuthenticationManager _authenticationManager = authenticationManager;
  readonly IRepository<User> _users = context.Repository<User>();

  public bool RequiresAuthorization()
  {
    return _users.Count(u => u.AccessLevel == AccessLevel.Administrator) == 0;
  }

  public ClaimsIdentity? Authorize(string token)
  {
    var user = _authenticationManager.AuthenticateToken(token);
    if (user == null)
      return null;

    var claims = new[]
    {
      new Claim(ClaimTypes.Name, user.Username ?? string.Empty),
      new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new Claim(ClaimTypes.Role, user.AccessLevel.ToString()),
    };

    // The second parameter (authenticationType) is required for IsAuthenticated to return true
    return new ClaimsIdentity(claims, "Bearer");
  }
}
