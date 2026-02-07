using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Users;

public class UserManager(IDataContext context) : IUserManager
{
  readonly IRepository<User> _users = context.Repository<User>();

  public UserDisplay CreateUser(UserDisplay userModel, UserDisplay? createdBy = null)
  {
    if (userModel == null)
    {
      throw new OverseerException("invalid_user");
    }

    if (string.IsNullOrWhiteSpace(userModel.Username))
    {
      throw new OverseerException("invalid_username");
    }

    // Users are always created by admins.
    // Admins will create username & password for readonly users.
    // The first admin must have a username/password, the created by will be null
    // since that gets created on initial setup.
    // Normal users and additional admins only require a username because
    // a magic link is used to allow them to set their password.
    if ((createdBy is null || userModel.AccessLevel == AccessLevel.Readonly) && string.IsNullOrWhiteSpace(userModel.Password))
    {
      throw new OverseerException("invalid_password");
    }

    if (_users.Exist(u => u.Username!.Equals(userModel.Username, StringComparison.OrdinalIgnoreCase)))
    {
      throw new OverseerException("unavailable_username");
    }

    string? hash;
    if (userModel.Password == null)
    {
      hash = null;
    }
    else
    {
      var salt = BCrypt.Net.BCrypt.GenerateSalt();
      hash = BCrypt.Net.BCrypt.HashPassword(userModel.Password, salt);
    }

    var user = new User
    {
      Username = userModel.Username,
      PasswordHash = hash,
      SessionLifetime = userModel.SessionLifetime,
      AccessLevel = userModel.AccessLevel,
    };

    _users.Create(user);

    return user.ToDisplay();
  }

  public IReadOnlyList<UserDisplay> GetUsers()
  {
    return _users.GetAll().Select(user => user.ToDisplay()).ToList();
  }

  public UserDisplay GetUser(int userId)
  {
    return _users.GetById(userId).ToDisplay();
  }

  public UserDisplay? UpdateUser(UserDisplay userModel)
  {
    var user = _users.GetById(userModel.Id);
    if (user == null)
    {
      return null;
    }

    //forces a new login if the session lifetime changes
    user.TokenHash = null;
    user.SessionLifetime = userModel.SessionLifetime;
    user.AccessLevel = userModel.AccessLevel;
    _users.Update(user);

    return user.ToDisplay();
  }

  public UserDisplay ChangePassword(UserDisplay userModel, UserDisplay changedBy)
  {
    if (string.IsNullOrWhiteSpace(userModel.Password))
    {
      throw new OverseerException("invalid_password");
    }

    var user = _users.GetById(userModel.Id);
    // readonly users can't change their own password, only admins can change readonly user passwords
    if (changedBy.AccessLevel != AccessLevel.Administrator && user.AccessLevel == AccessLevel.Readonly)
    {
      throw new OverseerException("invalid_user");
    }

    if (changedBy.AccessLevel != AccessLevel.Administrator && changedBy.Id != userModel.Id)
    {
      throw new OverseerException("invalid_user");
    }

    var salt = BCrypt.Net.BCrypt.GenerateSalt();
    var hash = BCrypt.Net.BCrypt.HashPassword(userModel.Password, salt);

    user.PasswordHash = hash;
    // if the user is changing their own password, keep them logged in
    // if it's an admin changing another user's password, force a re-login
    if (changedBy.AccessLevel == AccessLevel.Administrator && changedBy.Id != user.Id)
    {
      user.TokenHash = null;
    }

    _users.Update(user);

    return user.ToDisplay();
  }

  public void DeleteUser(int userId)
  {
    var users = _users.GetAll();
    if (users.Count == 1)
      throw new OverseerException("delete_user_unavailable");

    var user = users.FirstOrDefault(u => u.Id == userId);
    if (user?.AccessLevel == AccessLevel.Administrator && users.Count(u => u.AccessLevel == AccessLevel.Administrator) == 1)
      throw new OverseerException("delete_user_unavailable");

    _users.Delete(userId);
  }
}
