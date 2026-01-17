using Overseer.Server.Data;
using Overseer.Server.Models;

namespace Overseer.Server.Users;

public class UserManager(IDataContext context) : IUserManager
{
  readonly IRepository<User> _users = context.Repository<User>();

  public UserDisplay CreateUser(UserDisplay userModel)
  {
    if (string.IsNullOrWhiteSpace(userModel.Username))
    {
      throw new OverseerException("invalid_username");
    }
    ;

    if (string.IsNullOrWhiteSpace(userModel.Password))
    {
      throw new OverseerException("invalid_password");
    }

    if (_users.Exist(u => u.Username!.ToLower() == userModel.Username.ToLower()))
    {
      throw new OverseerException("unavailable_username");
    }

    var salt = BCrypt.Net.BCrypt.GenerateSalt();
    var hash = BCrypt.Net.BCrypt.HashPassword(userModel.Password, salt);

    var user = new User
    {
      Username = userModel.Username,
      PasswordSalt = salt,
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
    user.Token = null;
    user.TokenHash = null;
    user.TokenExpiration = null;
    user.SessionLifetime = userModel.SessionLifetime;
    user.AccessLevel = userModel.AccessLevel;
    _users.Update(user);

    return user.ToDisplay();
  }

  public UserDisplay ChangePassword(UserDisplay userModel)
  {
    if (string.IsNullOrWhiteSpace(userModel.Password))
    {
      throw new OverseerException("invalid_password");
    }

    var salt = BCrypt.Net.BCrypt.GenerateSalt();
    var hash = BCrypt.Net.BCrypt.HashPassword(userModel.Password, salt);

    var user = _users.GetById(userModel.Id);
    user.PasswordSalt = salt;
    user.PasswordHash = hash;
    user.Token = null;
    user.TokenHash = null;
    user.TokenExpiration = null;
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
