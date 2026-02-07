using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Users;

public class AuthorizationManager(IDataContext context) : IAuthorizationManager
{
  readonly IRepository<User> _users = context.Repository<User>();

  public bool RequiresAuthorization()
  {
    return _users.Count(u => u.AccessLevel == AccessLevel.Administrator) == 0;
  }
}
